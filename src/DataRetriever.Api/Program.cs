// Starts the ASP.NET Core host and wires the service endpoints.
using DataRetriever.Api;
using DataRetriever.Api.Composition;
using DataRetriever.Api.Configuration;
using DataRetriever.Api.Endpoints;
using DataRetriever.Api.Hosting;
using Microsoft.Extensions.Options;
using Serilog;

var startupLogging = ApplicationLogging.ConfigureBootstrapLogger(args);

try
{
    startupLogging.EnsureLaunchIsValid();

    var builder = WebApplication.CreateBuilder(args);

    // Keep container mistakes visible in every environment. Options values are validated
    // separately when the host starts.
    builder.Host.UseDefaultServiceProvider(options =>
    {
        options.ValidateOnBuild = true;
        options.ValidateScopes = true;
    });

    // Replace the bootstrap configuration after the container is available. Both phases
    // use the same destinations, so early and normal events share one rolling log file.
    builder.Services.AddSerilog((services, loggerConfiguration) =>
        ApplicationLogging.ConfigureFinalLogger(
            services,
            loggerConfiguration,
            builder.Configuration,
            startupLogging.LogFilePath));

    builder.Services.AddDataRetrieverApi(builder.Configuration);

    var app = builder.Build();

    app.MapGet(
        "/",
        (IOptions<ApplicationOptions> options) => Results.Ok(new { service = options.Value.Name }));
    app.MapDataRetrievalEndpoints();
    app.MapHealthChecks("/health");

    await app.RunAsync();
    return 0;
}
catch (OptionsValidationException exception)
{
    Log.Fatal(
        exception,
        "Application configuration is invalid: {ValidationFailures}",
        exception.Failures);
    throw;
}
catch (Exception exception)
{
    Log.Fatal(exception, "Application failed during startup or execution.");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

public partial class Program;

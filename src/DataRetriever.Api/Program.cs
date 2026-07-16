// Starts the ASP.NET Core host and wires the service endpoints.
using DataRetriever.Api;
using DataRetriever.Api.Composition;
using DataRetriever.Api.Configuration;
using DataRetriever.Api.Endpoints;
using DataRetriever.Api.Hosting;
using Microsoft.Extensions.Options;
using Serilog;

var logDirectory = StartupLogging.ConfigureBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Keep container mistakes visible in every environment. Options values are validated
    // separately when the host starts.
    builder.Host.UseDefaultServiceProvider(options =>
    {
        options.ValidateOnBuild = true;
        options.ValidateScopes = true;
    });

    builder.Services.AddSerilog((services, loggerConfiguration) => loggerConfiguration
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(
            Path.Combine(logDirectory, "application-.log"),
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 14,
            fileSizeLimitBytes: 10 * 1024 * 1024,
            rollOnFileSizeLimit: true,
            shared: true));

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
    return 1;
}
catch (Exception exception)
{
    Log.Fatal(exception, "Application failed during startup or execution.");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

public partial class Program;

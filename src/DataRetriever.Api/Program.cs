// Starts the ASP.NET Core host and wires the service endpoints.
using DataRetriever.Api;
using DataRetriever.Api.Composition;
using DataRetriever.Api.Endpoints;
using DataRetriever.Api.Hosting;
using Microsoft.Extensions.Options;
using Serilog;

var bootstrapLogger = ApplicationLogging.CreateFallbackBootstrapLogger();
Log.Logger = bootstrapLogger;

try
{
    var builder = WebApplication.CreateBuilder(args);
    var launch = builder.AddApplicationConfiguration(args);
    builder.ConfigureApplicationHost(launch, bootstrapLogger);

    var adapterMode = AdapterModeConfiguration.ReadRequired(builder.Configuration);
    builder.Services.AddDataRetrieverApi(builder.Configuration, adapterMode);

    var app = builder.Build();
    app.MapApplicationEndpoints();

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

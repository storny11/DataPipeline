// Starts the ASP.NET Core host and wires the service endpoints.
using DataRetriever.Api;
using DataRetriever.Api.Composition;
using DataRetriever.Api.Endpoints;
using DataRetriever.Api.Hosting;
using Microsoft.Extensions.Options;
using Serilog;

var bootstrapLogger = ApplicationLogging.CreateBootstrapLogger();
Log.Logger = bootstrapLogger;

try
{
    var bootstrapLogFilePath = ApplicationLogPath.ResolveBootstrapFilePath(args);
    ApplicationLogging.EnableBootstrapFile(bootstrapLogger, bootstrapLogFilePath);

    var environmentName = ApplicationEnvironment.ReadRequired(args);
    ApplicationLogPath.EnsureLaunchIsValid(args, environmentName);

    var builder = WebApplication.CreateBuilder(new WebApplicationOptions
    {
        Args = args,
        ContentRootPath = AppContext.BaseDirectory,
        EnvironmentName = environmentName
    });
    builder.AddApplicationConfiguration(args, bootstrapLogFilePath);
    builder.ConfigureApplicationHost();

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

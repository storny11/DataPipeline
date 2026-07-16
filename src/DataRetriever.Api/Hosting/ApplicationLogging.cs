// Configures one durable Serilog flow for both early startup and normal host logging.
using Serilog;

namespace DataRetriever.Api.Hosting;

internal static class ApplicationLogging
{
    public static StartupLoggingContext ConfigureBootstrapLogger(string[] args)
    {
        ApplicationLogPathResolution resolution;

        try
        {
            resolution = ApplicationLogPath.Resolve(args);
        }
        catch (Exception exception)
        {
            ConfigureConsoleBootstrapLogger();
            Log.Warning(exception, "The application log path could not be resolved.");

            return new StartupLoggingContext(
                LogFilePath: null,
                ValidationError: "The application log path could not be resolved.");
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(resolution.LogFilePath)!);

            Log.Logger = WriteToDestinations(
                    new LoggerConfiguration()
                        .MinimumLevel.Information()
                        .Enrich.FromLogContext(),
                    resolution.LogFilePath)
                .CreateBootstrapLogger();

            return new StartupLoggingContext(
                resolution.LogFilePath,
                resolution.ValidationError);
        }
        catch (Exception exception)
        {
            ConfigureConsoleBootstrapLogger();

            Log.Warning(
                exception,
                "The application log file could not be opened; logging will use the console only.");

            return new StartupLoggingContext(
                LogFilePath: null,
                resolution.ValidationError);
        }
    }

    public static void ConfigureFinalLogger(
        IServiceProvider services,
        LoggerConfiguration loggerConfiguration,
        IConfiguration configuration,
        string? logFilePath)
    {
        WriteToDestinations(
            loggerConfiguration
                .ReadFrom.Configuration(configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext(),
            logFilePath);
    }

    private static LoggerConfiguration WriteToDestinations(
        LoggerConfiguration loggerConfiguration,
        string? logFilePath)
    {
        loggerConfiguration.WriteTo.Console();

        if (!string.IsNullOrWhiteSpace(logFilePath))
        {
            loggerConfiguration.WriteTo.File(
                logFilePath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                fileSizeLimitBytes: 10 * 1024 * 1024,
                rollOnFileSizeLimit: true,
                shared: true);
        }

        return loggerConfiguration;
    }

    private static void ConfigureConsoleBootstrapLogger()
    {
        Log.Logger = WriteToDestinations(
                new LoggerConfiguration()
                    .MinimumLevel.Information()
                    .Enrich.FromLogContext(),
                logFilePath: null)
            .CreateBootstrapLogger();
    }
}

internal sealed record StartupLoggingContext(
    string? LogFilePath,
    string? ValidationError)
{
    public void EnsureLaunchIsValid()
    {
        if (!string.IsNullOrWhiteSpace(ValidationError))
        {
            throw new InvalidOperationException(ValidationError);
        }
    }
}

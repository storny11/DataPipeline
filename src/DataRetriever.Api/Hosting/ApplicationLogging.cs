// Configures one durable Serilog flow for both early startup and normal host logging.
using Serilog;

namespace DataRetriever.Api.Hosting;

internal static class ApplicationLogging
{
    private const string LogDirectoryEnvironmentVariable = "LOG_DIRECTORY";
    private const string LogFileName = "application-.log";

    public static string? ConfigureBootstrapLogger()
    {
        try
        {
            var logFilePath = ResolveLogFilePath();
            Directory.CreateDirectory(Path.GetDirectoryName(logFilePath)!);

            Log.Logger = WriteToDestinations(
                    new LoggerConfiguration()
                        .MinimumLevel.Information()
                        .Enrich.FromLogContext(),
                    logFilePath)
                .CreateBootstrapLogger();

            return logFilePath;
        }
        catch (Exception exception)
        {
            Log.Logger = WriteToDestinations(
                    new LoggerConfiguration()
                        .MinimumLevel.Information()
                        .Enrich.FromLogContext(),
                    logFilePath: null)
                .CreateBootstrapLogger();

            Log.Warning(
                exception,
                "The application log file could not be opened; logging will use the console only.");

            return null;
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

    private static string ResolveLogFilePath()
    {
        var configuredDirectory = Environment.GetEnvironmentVariable(LogDirectoryEnvironmentVariable);
        var logDirectory = string.IsNullOrWhiteSpace(configuredDirectory)
            ? Path.Combine(AppContext.BaseDirectory, "logs")
            : Path.GetFullPath(configuredDirectory, AppContext.BaseDirectory);

        return Path.Combine(logDirectory, LogFileName);
    }
}

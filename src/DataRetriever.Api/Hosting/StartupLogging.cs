// Creates a self-contained early logger that survives failures before the host logger exists.
using Serilog;

namespace DataRetriever.Api.Hosting;

internal static class StartupLogging
{
    private const string LogDirectoryEnvironmentVariable = "STARTUP_LOG_DIRECTORY";

    public static string ConfigureBootstrapLogger()
    {
        var fallbackDirectory = Path.Combine(AppContext.BaseDirectory, "logs");

        try
        {
            var configuredDirectory = Environment.GetEnvironmentVariable(LogDirectoryEnvironmentVariable);
            var logDirectory = string.IsNullOrWhiteSpace(configuredDirectory)
                ? fallbackDirectory
                : Path.GetFullPath(configuredDirectory, AppContext.BaseDirectory);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Console()
                .WriteTo.File(
                    Path.Combine(logDirectory, "startup-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    fileSizeLimitBytes: 10 * 1024 * 1024,
                    rollOnFileSizeLimit: true,
                    shared: true)
                .CreateBootstrapLogger();

            return logDirectory;
        }
        catch (Exception exception)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Console()
                .CreateBootstrapLogger();

            Log.Warning(
                exception,
                "The startup log file could not be opened; startup diagnostics will use the console only.");

            return fallbackDirectory;
        }
    }
}

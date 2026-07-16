// Configures one Serilog flow for both early startup and normal host logging.
using Serilog.Extensions.Hosting;
using Serilog;

namespace DataRetriever.Api.Hosting;

internal static class ApplicationLogging
{
    private const string FileSinkSectionName = "Serilog:WriteTo:FileSink";
    private const string FileSinkPathKey = $"{FileSinkSectionName}:Args:path";

    public static ReloadableLogger CreateFallbackBootstrapLogger()
    {
        return ConfigureFallback(new LoggerConfiguration())
            .CreateBootstrapLogger();
    }

    public static void ApplyResolvedLogFilePath(
        ConfigurationManager configuration,
        string logFilePath)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(logFilePath);

        configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [FileSinkPathKey] = logFilePath
        });
    }

    public static bool TryConfigureBootstrapLogger(
        ReloadableLogger bootstrapLogger,
        ConfigurationManager configuration)
    {
        ArgumentNullException.ThrowIfNull(bootstrapLogger);
        ArgumentNullException.ThrowIfNull(configuration);

        try
        {
            var fileSink = configuration.GetRequiredSection(FileSinkSectionName);
            if (!string.Equals(fileSink["Name"], "File", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"'{FileSinkSectionName}:Name' must select the Serilog File sink.");
            }

            var logFilePath = configuration[FileSinkPathKey];
            if (string.IsNullOrWhiteSpace(logFilePath))
            {
                throw new InvalidOperationException(
                    $"Required logging value '{FileSinkPathKey}' is missing.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(logFilePath)!);
            bootstrapLogger.Reload(loggerConfiguration =>
                loggerConfiguration.ReadFrom.Configuration(configuration));

            return true;
        }
        catch (Exception exception)
        {
            Log.Warning(
                exception,
                "Configuration-based logging could not be initialized; logging will use the console only.");

            return false;
        }
    }

    public static void ConfigureFinalLogger(
        IServiceProvider services,
        LoggerConfiguration loggerConfiguration,
        IConfiguration configuration,
        bool configurationLoggingAvailable)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(loggerConfiguration);
        ArgumentNullException.ThrowIfNull(configuration);

        if (!configurationLoggingAvailable)
        {
            ConfigureFallback(loggerConfiguration);
            return;
        }

        loggerConfiguration
            .ReadFrom.Configuration(configuration)
            .ReadFrom.Services(services);
    }

    private static LoggerConfiguration ConfigureFallback(
        LoggerConfiguration loggerConfiguration)
    {
        return loggerConfiguration
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .WriteTo.Console();
    }
}

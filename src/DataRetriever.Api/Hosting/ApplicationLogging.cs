// Configures one Serilog flow for both early startup and normal host logging.
using Serilog.Extensions.Hosting;
using Serilog;

namespace DataRetriever.Api.Hosting;

internal static class ApplicationLogging
{
    private const int RetainedFileCountLimit = 14;
    private const long FileSizeLimitBytes = 10 * 1024 * 1024;
    private const string FileSinkSectionName = "Serilog:WriteTo:FileSink";
    private const string FileSinkPathKey = $"{FileSinkSectionName}:Args:path";

    public static ReloadableLogger CreateBootstrapLogger()
    {
        return ConfigureConsole(new LoggerConfiguration())
            .CreateBootstrapLogger();
    }

    public static void EnableBootstrapFile(
        ReloadableLogger bootstrapLogger,
        string logFilePath)
    {
        ArgumentNullException.ThrowIfNull(bootstrapLogger);
        ArgumentException.ThrowIfNullOrWhiteSpace(logFilePath);

        Directory.CreateDirectory(Path.GetDirectoryName(logFilePath)!);
        bootstrapLogger.Reload(loggerConfiguration =>
            ConfigureConsole(loggerConfiguration)
                .WriteTo.File(
                    logFilePath,
                    rollingInterval: RollingInterval.Infinite,
                    retainedFileCountLimit: RetainedFileCountLimit,
                    fileSizeLimitBytes: FileSizeLimitBytes,
                    rollOnFileSizeLimit: true,
                    shared: true));
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

    public static void EnsureFileSinkIsConfigured(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var fileSink = configuration.GetRequiredSection(FileSinkSectionName);
        if (!string.Equals(fileSink["Name"], "File", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"'{FileSinkSectionName}:Name' must select the Serilog File sink.");
        }

        if (string.IsNullOrWhiteSpace(configuration[FileSinkPathKey]))
        {
            throw new InvalidOperationException(
                $"Required logging value '{FileSinkPathKey}' is missing.");
        }
    }

    public static void ConfigureFinalLogger(
        IServiceProvider services,
        LoggerConfiguration loggerConfiguration,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(loggerConfiguration);
        ArgumentNullException.ThrowIfNull(configuration);

        loggerConfiguration
            .ReadFrom.Configuration(configuration)
            .ReadFrom.Services(services);
    }

    private static LoggerConfiguration ConfigureConsole(
        LoggerConfiguration loggerConfiguration)
    {
        return loggerConfiguration
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .WriteTo.Console();
    }
}

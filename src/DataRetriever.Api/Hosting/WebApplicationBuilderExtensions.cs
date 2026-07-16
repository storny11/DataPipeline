// Configures process-wide host infrastructure before application modules are registered.
using Serilog;
using Serilog.Extensions.Hosting;

namespace DataRetriever.Api.Hosting;

internal static class WebApplicationBuilderExtensions
{
    public static WebApplicationBuilder ConfigureApplicationHost(
        this WebApplicationBuilder builder,
        string[] args,
        ReloadableLogger bootstrapLogger)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(bootstrapLogger);

        var logPath = ApplicationLogPath.Resolve(args, builder.Environment.EnvironmentName);
        var configurationLoggingAvailable = ApplicationLogging.TryConfigureBootstrapLogger(
            bootstrapLogger,
            builder.Configuration,
            logPath.LogFilePath);

        // Reject invalid launch input only after a durable logger has been attempted.
        logPath.EnsureLaunchIsValid();

        builder.Host.UseDefaultServiceProvider(options =>
        {
            options.ValidateOnBuild = true;
            options.ValidateScopes = true;
        });

        // The final logger reads the same configuration and resolved file path as bootstrap logging.
        builder.Services.AddSerilog((services, loggerConfiguration) =>
            ApplicationLogging.ConfigureFinalLogger(
                services,
                loggerConfiguration,
                builder.Configuration,
                configurationLoggingAvailable));

        return builder;
    }
}

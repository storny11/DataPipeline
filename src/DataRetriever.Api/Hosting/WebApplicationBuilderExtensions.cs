// Configures process-wide host infrastructure before application modules are registered.
using Serilog;
using Serilog.Extensions.Hosting;

namespace DataRetriever.Api.Hosting;

internal static class WebApplicationBuilderExtensions
{
    public static ApplicationLogPathResolution AddApplicationConfiguration(
        this WebApplicationBuilder builder,
        string[] args)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(args);

        // CreateBuilder has already added the standard providers. This is the application-specific,
        // highest-priority generated value required by both bootstrap and final logging.
        var launch = ApplicationLogPath.Resolve(args, builder.Environment.EnvironmentName);
        ApplicationLogging.ApplyResolvedLogFilePath(
            builder.Configuration,
            launch.LogFilePath);

        return launch;
    }

    public static WebApplicationBuilder ConfigureApplicationHost(
        this WebApplicationBuilder builder,
        ApplicationLogPathResolution launch,
        ReloadableLogger bootstrapLogger)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(launch);
        ArgumentNullException.ThrowIfNull(bootstrapLogger);

        var configurationLoggingAvailable = ApplicationLogging.TryConfigureBootstrapLogger(
            bootstrapLogger,
            builder.Configuration);

        // Reject invalid launch input only after a durable logger has been attempted.
        launch.EnsureLaunchIsValid();

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

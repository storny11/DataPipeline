// Configures process-wide host infrastructure before application modules are registered.
using Serilog;

namespace DataRetriever.Api.Hosting;

internal static class WebApplicationBuilderExtensions
{
    public static WebApplicationBuilder AddApplicationConfiguration(
        this WebApplicationBuilder builder,
        string[] args,
        string logFilePath,
        Action<ConfigurationManager>? addExternalConfiguration = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(logFilePath);

        // CreateBuilder has already loaded base/environment JSON and made bootstrap CLI values
        // available. Insert application-specific providers, then replay the normal runtime
        // overrides so the final precedence is external < local < environment < command line.
        addExternalConfiguration?.Invoke(builder.Configuration);

        builder.Configuration
            .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .AddCommandLine(args);

        // This generated safety value is intentionally higher priority than user configuration.
        ApplicationLogging.ApplyResolvedLogFilePath(
            builder.Configuration,
            logFilePath);

        return builder;
    }

    public static WebApplicationBuilder ConfigureApplicationHost(
        this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ApplicationLogging.EnsureFileSinkIsConfigured(builder.Configuration);

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
                builder.Configuration));

        return builder;
    }
}

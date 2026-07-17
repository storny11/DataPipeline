// Configures process-wide host infrastructure before application modules are registered.
using Serilog;

namespace DataRetriever.Api.Hosting;

internal static class WebApplicationBuilderExtensions
{
    private static readonly Dictionary<string, string> CommandLineMappings =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["--application-name"] = "Application:Name",
            ["--adapter-mode"] = "AdapterMode",
            ["--log-level"] = "Serilog:MinimumLevel:Default"
        };

    public static WebApplicationBuilder AddApplicationConfiguration(
        this WebApplicationBuilder builder,
        string[] args,
        string logFilePath,
        ApplicationLaunchArguments launchArguments,
        Action<ConfigurationManager, string>? addExternalConfiguration = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(launchArguments);
        ArgumentException.ThrowIfNullOrWhiteSpace(logFilePath);

        var externalConfigurationProfile = launchArguments.ExternalConfigurationProfile;
        if (addExternalConfiguration is null)
        {
            if (!string.IsNullOrWhiteSpace(externalConfigurationProfile))
            {
                throw new InvalidOperationException(
                    $"The '--{ApplicationLaunchArguments.ExternalConfigurationProfileArgumentName}' " +
                    "command-line argument was supplied, but no external configuration provider is registered.");
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(externalConfigurationProfile))
            {
                throw new InvalidOperationException(
                    $"The '--{ApplicationLaunchArguments.ExternalConfigurationProfileArgumentName}' " +
                    "command-line argument is required when external configuration is enabled.");
            }

            addExternalConfiguration(
                builder.Configuration,
                externalConfigurationProfile);
        }

        // CreateBuilder has already loaded base and environment JSON. The environment file is
        // deliberately reapplied after the external provider so checked-in environment values
        // can override external defaults. Canonical command-line mappings remain the final user
        // override layer.
        builder.Configuration
            .AddJsonFile(
                $"appsettings.{builder.Environment.EnvironmentName}.json",
                optional: true,
                reloadOnChange: false)
            .AddCommandLine(args, CommandLineMappings);

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
            // Some external registrations are completed lazily and are not safe for eager
            // whole-container construction. Keep scope validation and cover owned registrations
            // with focused composition tests.
            options.ValidateOnBuild = false;
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

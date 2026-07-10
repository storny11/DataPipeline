// Core reporting registration is delivery-agnostic. The built-in SMTP publisher is an
// explicit opt-in; custom publishers are ordinary singleton registrations.
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace RunReporting;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRunReporting(
        this IServiceCollection services,
        Action<RunReportingOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new RunReportingOptions();
        configure?.Invoke(options);
        RegisterCore(services, options);
        return services;
    }

    /// <summary>Adds the built-in SMTP publisher configured from the required "EmailReport" section.</summary>
    public static IServiceCollection AddSmtpRunReportPublisher(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<RunReportingOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        return services.AddSmtpRunReportPublisher(options =>
        {
            var section = configuration.GetSection(RunReportingOptions.SectionName);
            if (!section.Exists())
            {
                throw new InvalidOperationException(
                    $"SMTP run reporting requires an '{RunReportingOptions.SectionName}' configuration section " +
                    "(Enabled, Host, Port, From, To, ...).");
            }

            section.Bind(options);
            configure?.Invoke(options);
        });
    }

    /// <summary>Adds the built-in SMTP publisher with explicitly configured options.</summary>
    public static IServiceCollection AddSmtpRunReportPublisher(
        this IServiceCollection services,
        Action<RunReportingOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new RunReportingOptions();
        configure?.Invoke(options);
        var errors = options.GetValidationErrors();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                $"Run reporting email configuration is invalid: {string.Join("; ", errors)}. " +
                "Fix the configuration or set Enabled=false to run without SMTP email.");
        }

        RegisterCore(services, options);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IRunReportPublisher, EmailRunReportPublisher>());
        return services;
    }

    private static void RegisterCore(IServiceCollection services, RunReportingOptions options)
    {
        services.AddLogging();
        services.RemoveAll<RunReportingOptions>();
        services.AddSingleton(options);
        services.TryAddSingleton<IRunReportFormatter, RazorRunReportFormatter>();
        services.TryAddScoped<IRunReporter, RunReporter>();
    }
}

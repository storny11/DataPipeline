// Registers run reporting as singletons. Extra publishers are plain
// AddSingleton<IRunReportPublisher, ...> registrations; all of them receive each report.
// Configuration problems fail fast here, at composition time — never during a run.
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace RunReporting;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers run reporting configured by convention from the required "EmailReport" configuration section.</summary>
    public static IServiceCollection AddRunReporting(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<RunReportingOptions>? configure = null)
    {
        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        return services.AddRunReporting(options =>
        {
            var section = configuration.GetSection(RunReportingOptions.SectionName);
            if (!section.Exists())
            {
                throw new InvalidOperationException(
                    $"Run reporting requires an '{RunReportingOptions.SectionName}' configuration section " +
                    "(Enabled, Host, Port, From, To, ...).");
            }

            section.Bind(options);
            configure?.Invoke(options);
        });
    }

    public static IServiceCollection AddRunReporting(
        this IServiceCollection services,
        Action<RunReportingOptions>? configure = null)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        var options = new RunReportingOptions();
        configure?.Invoke(options);

        var errors = options.GetValidationErrors();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                $"Run reporting email configuration is invalid: {string.Join("; ", errors)}. " +
                "Fix the configuration or set Enabled=false to run without email.");
        }

        // The Razor formatter and reporter need logging infrastructure even in hosts that never call AddLogging.
        services.AddLogging();

        services.RemoveAll<RunReportingOptions>();
        services.AddSingleton(options);
        services.TryAddSingleton<IRunReportFormatter, RazorRunReportFormatter>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IRunReportPublisher, EmailRunReportPublisher>());
        services.TryAddSingleton<IRunReporter, RunReporter>();

        return services;
    }

    /// <summary>Points the default Razor formatter at a typed custom template component.</summary>
    public static IServiceCollection UseRunReportTemplate<TTemplate>(this IServiceCollection services)
        where TTemplate : RunReportTemplateBase
    {
        services.AddSingleton(new RunReportTemplate(typeof(TTemplate)));
        return services;
    }
}

using DataRetriever.Api.Composition;
using DataRetriever.Application;
using DataRetriever.Infrastructure;
using DataRetriever.Monitoring;
using DataRetriever.Reporting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Text.Json.Serialization;

namespace DataRetriever.Api;

public static class ServiceCollectionExtensions
{
    // TODO: Replace this hardcoded local-test switch with explicit configuration before production use.
    private const bool UseRealEmailSenderWithSimulatorData = true;

    public static IServiceCollection AddDataRetrieverApi(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddHealthChecks();

        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

        services
            .AddDataRetrieverReporting(configuration)
            .AddDataRetrieverMonitoring()
            .AddDataRetrieverApplication();

        var adapterMode = AdapterModeOptions.FromConfiguration(configuration);
        if (adapterMode == AdapterMode.Real)
        {
            services.AddRealAdapters(configuration);
        }
        else
        {
            services.AddSimulatorAdapters();

            if (UseRealEmailSenderWithSimulatorData)
            {
                services.RemoveAll<IRunReportPublisher>();
                services.AddDataRetrieverEmailReporting(configuration);
            }
        }

        return services;
    }
}

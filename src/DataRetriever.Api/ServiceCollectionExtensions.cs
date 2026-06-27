using DataRetriever.Api.Composition;
using DataRetriever.Application;
using DataRetriever.Monitoring;
using DataRetriever.Reporting;
using System.Text.Json.Serialization;

namespace DataRetriever.Api;

public static class ServiceCollectionExtensions
{
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
            .AddDataRetrieverReporting()
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
        }

        return services;
    }
}

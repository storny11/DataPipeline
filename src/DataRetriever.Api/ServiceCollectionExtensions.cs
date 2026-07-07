// Composes the API host dependencies and chooses simulator or real adapter registration.
using DataRetriever.Api.Composition;
using DataRetriever.Application;
using DataRetriever.Monitoring;
using DataRetriever.Reporting;
using Microsoft.FeatureManagement;
using RunReporting;
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

        services.AddFeatureManagement(configuration);
        services.Configure<ConfigurationFeatureDefinitionProviderOptions>(options =>
        {
            options.CustomConfigurationMergingEnabled = true;
        });

        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

        services
            .AddRunReporting(configuration, options => options.ApplicationName = "Data retrieval")
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

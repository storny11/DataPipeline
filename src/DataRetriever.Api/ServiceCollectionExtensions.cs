// Composes the API host dependencies and chooses simulator or real adapter registration.
using DataRetriever.Api.Composition;
using DataRetriever.Api.Configuration;
using DataRetriever.Api.Hosting;
using DataRetriever.Application;
using DataRetriever.Monitoring;
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
        services.AddApplicationOptions(configuration);
        services.AddHostedService<StartupSummaryHostedService>();

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
            .AddRunReporting()
            .AddSmtpRunReportPublisher(configuration, options =>
            {
                if (string.IsNullOrWhiteSpace(options.ServiceName))
                {
                    options.ServiceName = "Data retrieval";
                }
            })
            .AddDataRetrieverMonitoring()
            .AddDataRetrieverApplication();

        var adapterMode = AdapterModeConfiguration.ReadRequired(configuration);
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

using DataRetriever.Api.Composition;
using DataRetriever.Application;
using DataRetriever.Execution;
using DataRetriever.Monitoring;
using DataRetriever.Reporting;

namespace DataRetriever.Api;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDataRetrieverApi(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddHealthChecks();

        services
            .AddDataRetrieverExecution()
            .AddDataRetrieverReporting()
            .AddDataRetrieverMonitoring()
            .AddDataRetrieverApplication();

        var adapterMode = AdapterModeOptions.FromConfiguration(configuration);
        if (adapterMode == AdapterMode.Real)
        {
            services.AddRealAdapters();
        }
        else
        {
            services.AddSimulatorAdapters();
        }

        return services;
    }
}

// Registers monitoring services for the host.
using Microsoft.Extensions.DependencyInjection;

namespace DataRetriever.Monitoring;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDataRetrieverMonitoring(this IServiceCollection services)
    {
        services.AddSingleton<IProcessingTracker, InMemoryProcessingTracker>();
        return services;
    }
}

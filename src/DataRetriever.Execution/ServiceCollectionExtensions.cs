using Microsoft.Extensions.DependencyInjection;

namespace DataRetriever.Execution;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDataRetrieverExecution(this IServiceCollection services)
    {
        return services;
    }
}

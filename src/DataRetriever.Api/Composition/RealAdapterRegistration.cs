using DataRetriever.Infrastructure;

namespace DataRetriever.Api.Composition;

public static class RealAdapterRegistration
{
    public static IServiceCollection AddRealAdapters(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        return services.AddDataRetrieverInfrastructure(configuration);
    }
}

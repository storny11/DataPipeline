using Microsoft.Extensions.DependencyInjection;

namespace DataRetriever.Infrastructure.Step3Load;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddStep3SourceClient(this IServiceCollection services)
    {
        services.AddOptions<Step3SourceClientOptions>();
        services.AddScoped<Step3ExternalClient>();
        return services;
    }
}

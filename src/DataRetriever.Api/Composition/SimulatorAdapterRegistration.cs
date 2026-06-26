using DataRetriever.Simulators;

namespace DataRetriever.Api.Composition;

public static class SimulatorAdapterRegistration
{
    public static IServiceCollection AddSimulatorAdapters(this IServiceCollection services)
    {
        return services.AddDataRetrieverSimulators();
    }
}

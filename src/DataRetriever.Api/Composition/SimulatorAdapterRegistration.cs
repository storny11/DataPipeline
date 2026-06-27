// Registers simulator adapters so local runs can exercise the application flow without real dependencies.
using DataRetriever.Simulators;

namespace DataRetriever.Api.Composition;

public static class SimulatorAdapterRegistration
{
    public static IServiceCollection AddSimulatorAdapters(this IServiceCollection services)
    {
        return services.AddDataRetrieverSimulators();
    }
}

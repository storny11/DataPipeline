using DataRetriever.Application.Step1Load;
using DataRetriever.Application.Step1Load.Models;

namespace DataRetriever.Simulators.Step1Load;

public sealed class Step1SourceSimulator(SimulatorSeedData seedData) : IStep1SourceClient
{
    public Task<IReadOnlyList<Step1Dto>> LoadConfiguredDataAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(seedData.ConfiguredRows);
    }
}

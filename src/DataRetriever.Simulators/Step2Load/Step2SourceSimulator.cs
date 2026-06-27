// Returns related Step 2 seed rows for simulator runs.
using DataRetriever.Application.Step1Load.Models;
using DataRetriever.Application.Step2Load;
using DataRetriever.Application.Step2Load.Models;

namespace DataRetriever.Simulators.Step2Load;

public sealed class Step2SourceSimulator(SimulatorSeedData seedData) : IStep2SourceClient
{
    public Task<IReadOnlyList<Step2ResponseDto>> FetchRelatedDataAsync(
        Step1OutputRecord input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.Equals(input.ExternalId1, "EXT1-FFF", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Simulated isolated source failure.");
        }

        return Task.FromResult(seedData.Step2Rows.TryGetValue(input.ExternalId1, out var rows)
            ? rows
            : []);
    }
}

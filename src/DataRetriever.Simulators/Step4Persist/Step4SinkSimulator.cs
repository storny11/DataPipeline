// Simulates persistence success or configured failure for Step 4.
using DataRetriever.Application.Step4Persist;
using DataRetriever.Application.Step4Persist.Models;

namespace DataRetriever.Simulators.Step4Persist;

public sealed class Step4SinkSimulator : IStep4SinkClient
{
    public Task<Step4PersistResult> PersistAsync(
        IReadOnlyList<Step4RequestDto> records,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (records.Any(record => string.Equals(record.ExternalId2, "EXT2-PERSIST-FAIL", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Simulated persistence failure.");
        }

        return Task.FromResult(Step4PersistResult.AllSucceeded(records.Count));
    }
}

using DataRetriever.Application.Step4Persist;
using DataRetriever.Application.Step4Persist.Models;

namespace DataRetriever.Simulators.Step4Persist;

public sealed class Step4SinkSimulator : IStep4SinkClient
{
    public Task<IReadOnlyList<Step4RequestDto>> PersistAsync(
        IReadOnlyList<Step4RequestDto> records,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (records.Any(record => string.Equals(record.ExternalId2, "EXT2-PERSIST-FAIL", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Simulated persistence failure.");
        }

        return Task.FromResult(records);
    }
}

// Defines the Step 4 sink boundary for persisting final records.
using DataRetriever.Application.Step4Persist.Models;

namespace DataRetriever.Application.Step4Persist;

public interface IStep4SinkClient
{
    Task<Step4PersistResult> PersistAsync(
        IReadOnlyList<Step4RequestDto> records,
        CancellationToken cancellationToken);
}

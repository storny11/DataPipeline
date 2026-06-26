using DataRetriever.Application.Step4Persist;
using DataRetriever.Application.Step4Persist.Models;

namespace DataRetriever.Infrastructure.Step4Persist;

public sealed class Step4SinkClient : IStep4SinkClient
{
    public Task<IReadOnlyList<Step4RequestDto>> PersistAsync(
        IReadOnlyList<Step4RequestDto> records,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException("Configure a real Step 4 sink client for non-simulator mode.");
    }
}

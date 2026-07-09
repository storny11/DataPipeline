// Placeholder real Step 1 client; the implementation should push the selection into its source query.
using DataRetriever.Application.Runs;
using DataRetriever.Application.Step1Load;
using DataRetriever.Application.Step1Load.Models;

namespace DataRetriever.Infrastructure.Step1Load;

public sealed class Step1SourceClient : IStep1SourceClient
{
    public Task<IReadOnlyList<Step1SourceRow>> LoadConfiguredDataAsync(
        DataRetrievalRunOptions selection,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException("Configure a real Step 1 source client for non-simulator mode.");
    }
}

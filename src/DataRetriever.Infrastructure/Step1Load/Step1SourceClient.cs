// Placeholder real Step 1 client to be replaced by an actual configured-data source.
using DataRetriever.Application.Step1Load;
using DataRetriever.Application.Step1Load.Models;

namespace DataRetriever.Infrastructure.Step1Load;

public sealed class Step1SourceClient : IStep1SourceClient
{
    public Task<IReadOnlyList<Step1Dto>> LoadConfiguredDataAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException("Configure a real Step 1 source client for non-simulator mode.");
    }
}

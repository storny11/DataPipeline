// Placeholder real Step 2 client to be replaced by an actual related-data source.
using DataRetriever.Application.Step1Load.Models;
using DataRetriever.Application.Step2Load;
using DataRetriever.Application.Step2Load.Models;

namespace DataRetriever.Infrastructure.Step2Load;

public sealed class Step2SourceClient : IStep2SourceClient
{
    public Task<IReadOnlyList<Step2ResponseDto>> FetchRelatedDataAsync(
        Step1OutputRecord input,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException("Configure a real Step 2 source client for non-simulator mode.");
    }
}

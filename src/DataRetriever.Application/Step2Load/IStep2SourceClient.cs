// Defines the Step 2 source boundary for fetching related rows per Step 1 record.
using DataRetriever.Application.Step1Load.Models;
using DataRetriever.Application.Step2Load.Models;

namespace DataRetriever.Application.Step2Load;

public interface IStep2SourceClient
{
    Task<IReadOnlyList<Step2ResponseDto>> FetchRelatedDataAsync(
        Step1OutputRecord input,
        CancellationToken cancellationToken);
}

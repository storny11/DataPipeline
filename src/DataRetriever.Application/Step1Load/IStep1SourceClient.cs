// Defines the Step 1 source boundary; implementations apply the run selection before returning rows.
using DataRetriever.Application.Runs;
using DataRetriever.Application.Step1Load.Models;

namespace DataRetriever.Application.Step1Load;

public interface IStep1SourceClient
{
    Task<IReadOnlyList<Step1SourceRow>> LoadConfiguredDataAsync(
        DataRetrievalRunOptions selection,
        CancellationToken cancellationToken);
}

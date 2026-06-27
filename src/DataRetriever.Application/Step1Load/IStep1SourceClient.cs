// Defines the Step 1 source boundary implemented by infrastructure or simulators.
using DataRetriever.Application.Step1Load.Models;

namespace DataRetriever.Application.Step1Load;

public interface IStep1SourceClient
{
    Task<IReadOnlyList<Step1Dto>> LoadConfiguredDataAsync(CancellationToken cancellationToken);
}

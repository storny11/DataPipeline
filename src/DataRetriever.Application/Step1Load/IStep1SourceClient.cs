using DataRetriever.Application.Step1Load.Models;

namespace DataRetriever.Application.Step1Load;

public interface IStep1SourceClient
{
    Task<IReadOnlyList<Step1Dto>> LoadConfiguredDataAsync(CancellationToken cancellationToken);
}

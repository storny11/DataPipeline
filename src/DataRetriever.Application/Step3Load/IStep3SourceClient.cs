using DataRetriever.Application.Step3Load.Models;

namespace DataRetriever.Application.Step3Load;

public interface IStep3SourceClient
{
    Task<Step3ResponseDto> FetchAmountsAsync(
        Step3RequestDto request,
        CancellationToken cancellationToken);
}

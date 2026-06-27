// Adapts the Step 3 external client to the application-owned source interface.
using DataRetriever.Application.Step3Load;
using DataRetriever.Application.Step3Load.Models;

namespace DataRetriever.Infrastructure.Step3Load;

public sealed class Step3SourceClient(Step3ExternalClient externalClient) : IStep3SourceClient
{
    public Task<Step3ResponseDto> FetchAmountsAsync(
        Step3RequestDto request,
        CancellationToken cancellationToken)
    {
        return externalClient.FetchAmountsAsync(request, cancellationToken);
    }
}

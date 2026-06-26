using DataRetriever.Application.Step3Load.Models;
using Microsoft.Extensions.Options;

namespace DataRetriever.Infrastructure.Step3Load;

public sealed class Step3ExternalClient(IOptions<Step3SourceClientOptions> options)
{
    public async Task<Step3ResponseDto> FetchAmountsAsync(
        Step3RequestDto request,
        CancellationToken cancellationToken)
    {
        Exception? lastException = null;

        for (var attempt = 1; attempt <= options.Value.MaxRetryAttempts; attempt++)
        {
            try
            {
                return await FetchWithGeneratedClientAsync(request, cancellationToken);
            }
            catch (Exception exception) when (IsTransient(exception) && attempt < options.Value.MaxRetryAttempts)
            {
                lastException = exception;
                var delay = TimeSpan.FromMilliseconds(options.Value.RetryBaseDelay.TotalMilliseconds * attempt);
                await Task.Delay(delay, cancellationToken);
            }
        }

        throw lastException ?? new InvalidOperationException("Step 3 external client failed.");
    }

    private static Task<Step3ResponseDto> FetchWithGeneratedClientAsync(
        Step3RequestDto request,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException("Configure the generated Step 3 client for non-simulator mode.");
    }

    private static bool IsTransient(Exception exception)
    {
        return exception is TimeoutException or HttpRequestException;
    }
}

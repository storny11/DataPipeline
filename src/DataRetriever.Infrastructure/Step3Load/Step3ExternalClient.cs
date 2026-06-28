// Wraps the future generated Step 3 client with retry behavior.
using DataRetriever.Application.Step3Load.Models;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using System.Net;

namespace DataRetriever.Infrastructure.Step3Load;

public sealed class Step3ExternalClient
{
    private readonly ResiliencePipeline<Step3ResponseDto> _retryPipeline;

    public Step3ExternalClient(IOptions<Step3SourceClientOptions> options)
    {
        _retryPipeline = new ResiliencePipelineBuilder<Step3ResponseDto>()
            .AddRetry(new RetryStrategyOptions<Step3ResponseDto>
            {
                ShouldHandle = arguments => new ValueTask<bool>(
                    arguments.Outcome.Exception is { } exception &&
                    IsTransientRequestException(exception, arguments.Context.CancellationToken)),
                MaxRetryAttempts = Math.Max(0, options.Value.MaxRetryAttempts - 1),
                Delay = options.Value.RetryBaseDelay,
                BackoffType = DelayBackoffType.Exponential
            })
            .Build();
    }

    public async Task<Step3ResponseDto> FetchAmountsAsync(
        Step3RequestDto request,
        CancellationToken cancellationToken)
    {
        return await _retryPipeline.ExecuteAsync(
            async token => await FetchWithGeneratedClientAsync(request, token),
            cancellationToken);
    }

    private static Task<Step3ResponseDto> FetchWithGeneratedClientAsync(
        Step3RequestDto request,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException("Configure the generated Step 3 client for non-simulator mode.");
    }

    private static bool IsTransientRequestException(Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        return exception switch
        {
            HttpRequestException httpRequestException => httpRequestException.StatusCode is null ||
                IsTransientStatusCode((int)httpRequestException.StatusCode.Value),
            TimeoutException => true,
            OperationCanceledException => true,
            ApiException apiException => IsTransientStatusCode(apiException.StatusCode),
            { InnerException: not null } => IsTransientRequestException(exception.InnerException, cancellationToken),
            _ => false
        };
    }

    private static bool IsTransientStatusCode(int statusCode)
    {
        return statusCode == (int)HttpStatusCode.RequestTimeout ||
            statusCode == 429 ||
            statusCode == (int)HttpStatusCode.InternalServerError ||
            statusCode == (int)HttpStatusCode.BadGateway ||
            statusCode == (int)HttpStatusCode.ServiceUnavailable ||
            statusCode == (int)HttpStatusCode.GatewayTimeout;
    }
}

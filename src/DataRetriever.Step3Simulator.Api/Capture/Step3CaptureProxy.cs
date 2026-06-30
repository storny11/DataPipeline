using Microsoft.Extensions.Options;

namespace DataRetriever.Step3Simulator.Api.Capture;

public sealed class Step3CaptureProxy(
    CaptureKeyBuilder keyBuilder,
    CaptureStore captureStore,
    IUpstreamStep3Client upstreamClient,
    IOptions<Step3CaptureOptions> options,
    ILogger<Step3CaptureProxy> logger)
{
    private readonly Step3CaptureOptions _options = options.Value;

    public async Task HandleAsync(HttpContext context, CancellationToken cancellationToken)
    {
        byte[] requestBody = await ReadRequestBodyAsync(context.Request, cancellationToken);
        string pathAndQuery = $"{context.Request.Path}{context.Request.QueryString}";
        string key = keyBuilder.Build(context.Request.Method, pathAndQuery, requestBody);

        CapturedHttpResponse response = _options.Mode switch
        {
            Step3CaptureMode.Capture => await CaptureAsync(context, requestBody, pathAndQuery, key, cancellationToken),
            Step3CaptureMode.Replay => await ReplayAsync(context, key, cancellationToken),
            _ => throw new InvalidOperationException($"Unsupported Step 3 capture mode '{_options.Mode}'.")
        };

        await WriteResponseAsync(context.Response, response, cancellationToken);
    }

    private async Task<CapturedHttpResponse> CaptureAsync(
        HttpContext context,
        byte[] requestBody,
        string pathAndQuery,
        string key,
        CancellationToken cancellationToken)
    {
        CapturedHttpResponse response = await upstreamClient.SendAsync(context, requestBody, cancellationToken);
        CapturedInteraction interaction = new(
            DateTimeOffset.UtcNow,
            context.Request.Method,
            pathAndQuery,
            context.Request.ContentType,
            Convert.ToBase64String(requestBody),
            response.StatusCode,
            response.ContentType,
            Convert.ToBase64String(response.Body));

        await captureStore.WriteAsync(key, interaction, cancellationToken);
        logger.LogInformation("Captured Step 3 response for {Method} {PathAndQuery} as {CaptureKey}.", context.Request.Method, pathAndQuery, key);

        return response;
    }

    private async Task<CapturedHttpResponse> ReplayAsync(
        HttpContext context,
        string key,
        CancellationToken cancellationToken)
    {
        CapturedInteraction? interaction = await captureStore.TryReadAsync(key, cancellationToken);
        if (interaction is null)
        {
            logger.LogWarning("No Step 3 capture found for {Method} {PathAndQuery} with key {CaptureKey}.", context.Request.Method, $"{context.Request.Path}{context.Request.QueryString}", key);

            const string missingCapture = "{\"error\":\"No Step 3 capture found for this request.\"}";
            return new CapturedHttpResponse(
                StatusCodes.Status404NotFound,
                "application/json",
                System.Text.Encoding.UTF8.GetBytes(missingCapture));
        }

        return new CapturedHttpResponse(
            interaction.StatusCode,
            interaction.ResponseContentType,
            Convert.FromBase64String(interaction.ResponseBodyBase64));
    }

    private static async Task<byte[]> ReadRequestBodyAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (request.Body is null)
        {
            return [];
        }

        using MemoryStream memoryStream = new();
        await request.Body.CopyToAsync(memoryStream, cancellationToken);
        return memoryStream.ToArray();
    }

    private static async Task WriteResponseAsync(
        HttpResponse response,
        CapturedHttpResponse capturedResponse,
        CancellationToken cancellationToken)
    {
        response.StatusCode = capturedResponse.StatusCode;
        if (!string.IsNullOrWhiteSpace(capturedResponse.ContentType))
        {
            response.ContentType = capturedResponse.ContentType;
        }

        await response.Body.WriteAsync(capturedResponse.Body, cancellationToken);
    }
}

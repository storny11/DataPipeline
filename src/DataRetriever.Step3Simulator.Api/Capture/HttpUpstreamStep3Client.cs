using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace DataRetriever.Step3Simulator.Api.Capture;

public sealed class HttpUpstreamStep3Client(
    HttpClient httpClient,
    IOptions<Step3CaptureOptions> options) : IUpstreamStep3Client
{
    private static readonly HashSet<string> SkippedHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        HeaderNames.Host,
        HeaderNames.ContentLength,
        HeaderNames.TransferEncoding,
        HeaderNames.Connection,
        HeaderNames.KeepAlive,
        HeaderNames.Upgrade,
        HeaderNames.ProxyAuthenticate,
        HeaderNames.ProxyAuthorization,
        HeaderNames.TE,
        HeaderNames.Trailer
    };

    private readonly HttpClient _httpClient = httpClient;
    private readonly Step3CaptureOptions _options = options.Value;

    public async Task<CapturedHttpResponse> SendAsync(
        HttpContext context,
        byte[] requestBody,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requestBody);

        if (string.IsNullOrWhiteSpace(_options.UpstreamBaseUrl))
        {
            throw new InvalidOperationException(
                $"{Step3CaptureOptions.SectionName}:{nameof(Step3CaptureOptions.UpstreamBaseUrl)} is required in Capture mode.");
        }

        using HttpRequestMessage request = CreateRequest(context, requestBody);
        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);

        byte[] responseBody = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        return new CapturedHttpResponse(
            (int)response.StatusCode,
            response.Content.Headers.ContentType?.ToString(),
            responseBody);
    }

    private HttpRequestMessage CreateRequest(HttpContext context, byte[] requestBody)
    {
        Uri upstreamUri = BuildUpstreamUri(context.Request.Path, context.Request.QueryString);
        HttpRequestMessage request = new(new HttpMethod(context.Request.Method), upstreamUri);

        foreach (var header in context.Request.Headers)
        {
            if (SkippedHeaders.Contains(header.Key))
            {
                continue;
            }

            if (!request.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()))
            {
                request.Content ??= new ByteArrayContent(requestBody);
                request.Content.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }
        }

        if (requestBody.Length > 0 || !string.IsNullOrWhiteSpace(context.Request.ContentType))
        {
            request.Content ??= new ByteArrayContent(requestBody);
            if (!string.IsNullOrWhiteSpace(context.Request.ContentType))
            {
                request.Content.Headers.TryAddWithoutValidation(HeaderNames.ContentType, context.Request.ContentType);
            }
        }

        return request;
    }

    private Uri BuildUpstreamUri(PathString path, QueryString queryString)
    {
        Uri baseUri = new(_options.UpstreamBaseUrl!, UriKind.Absolute);
        UriBuilder builder = new(baseUri)
        {
            Path = JoinPath(baseUri.AbsolutePath, path.Value ?? string.Empty),
            Query = queryString.HasValue ? queryString.Value![1..] : string.Empty
        };

        return builder.Uri;
    }

    private static string JoinPath(string basePath, string requestPath)
    {
        if (string.IsNullOrWhiteSpace(basePath) || basePath == "/")
        {
            return requestPath;
        }

        return $"{basePath.TrimEnd('/')}/{requestPath.TrimStart('/')}";
    }
}

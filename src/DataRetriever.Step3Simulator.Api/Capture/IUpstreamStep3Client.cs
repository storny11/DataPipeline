namespace DataRetriever.Step3Simulator.Api.Capture;

public interface IUpstreamStep3Client
{
    Task<CapturedHttpResponse> SendAsync(
        HttpContext context,
        byte[] requestBody,
        CancellationToken cancellationToken);
}

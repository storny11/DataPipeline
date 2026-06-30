namespace DataRetriever.Step3Simulator.Api.Capture;

public sealed record CapturedHttpResponse(
    int StatusCode,
    string? ContentType,
    byte[] Body);

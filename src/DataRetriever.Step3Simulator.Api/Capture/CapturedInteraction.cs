namespace DataRetriever.Step3Simulator.Api.Capture;

public sealed record CapturedInteraction(
    DateTimeOffset CapturedAtUtc,
    string Method,
    string PathAndQuery,
    string? RequestContentType,
    string RequestBodyBase64,
    int StatusCode,
    string? ResponseContentType,
    string ResponseBodyBase64);

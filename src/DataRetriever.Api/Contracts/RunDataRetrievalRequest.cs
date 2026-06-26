namespace DataRetriever.Api.Contracts;

public sealed record RunDataRetrievalRequest(
    string? Currency,
    IReadOnlyList<string>? InternalIds);

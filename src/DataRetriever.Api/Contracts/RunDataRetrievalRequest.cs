// Defines the API request filters accepted when starting a data retrieval run.
namespace DataRetriever.Api.Contracts;

public sealed record RunDataRetrievalRequest(
    string? Currency,
    IReadOnlyList<string>? InternalIds);

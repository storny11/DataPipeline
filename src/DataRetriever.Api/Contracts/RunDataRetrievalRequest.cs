// Defines the raw API request filters accepted when starting a data retrieval run.
namespace DataRetriever.Api.Contracts;

public sealed record RunDataRetrievalRequest(
    string? Currency,
    string? InternalIds)
{
    public IReadOnlyList<string> SplitInternalIds()
    {
        return string.IsNullOrWhiteSpace(InternalIds)
            ? []
            : InternalIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}

// Maps the raw API run request into application run options and boundary validation errors.
using DataRetriever.Application.Runs;

namespace DataRetriever.Api.Contracts;

public static class RunDataRetrievalRequestMapper
{
    public static bool TryMap(
        RunDataRetrievalRequest? request,
        out DataRetrievalRunOptions options,
        out string? errorMessage)
    {
        request ??= new RunDataRetrievalRequest(null, null);

        var internalIds = SplitInternalIds(request.InternalIds);
        if (!string.IsNullOrWhiteSpace(request.Currency) && internalIds.Count > 0)
        {
            options = DataRetrievalRunOptions.All;
            errorMessage = "Provide either currency or internalIds, not both.";
            return false;
        }

        options = DataRetrievalRunOptions.FromRequest(request.Currency, internalIds);
        errorMessage = null;
        return true;
    }

    private static IReadOnlyList<string> SplitInternalIds(string? internalIds)
    {
        return string.IsNullOrWhiteSpace(internalIds)
            ? []
            : internalIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}

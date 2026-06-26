using DataRetriever.Api.Contracts;
using DataRetriever.Application.Runs;

namespace DataRetriever.Api.Endpoints;

public static class RunDataRetrievalEndpoint
{
    public static async Task<IResult> HandleAsync(
        RunDataRetrievalRequest? request,
        DataRetrievalOrchestrator orchestrator,
        SingleRunGuard singleRunGuard,
        CancellationToken cancellationToken)
    {
        request ??= new RunDataRetrievalRequest(null, null);

        var hasCurrency = !string.IsNullOrWhiteSpace(request.Currency);
        var hasInternalIds = request.InternalIds?.Any(id => !string.IsNullOrWhiteSpace(id)) == true;
        if (hasCurrency && hasInternalIds)
        {
            return Results.BadRequest(new
            {
                message = "Provide either currency or internalIds, not both."
            });
        }

        using var lease = await singleRunGuard.TryEnterAsync(cancellationToken);
        if (lease is null)
        {
            return Results.Conflict(new
            {
                message = "A data retrieval run is already in progress."
            });
        }

        var options = DataRetrievalRunOptions.FromRequest(request.Currency, request.InternalIds);
        var report = await orchestrator.RunAsync(options, cancellationToken);
        return Results.Ok(report);
    }
}

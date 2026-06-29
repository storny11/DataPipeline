// Validates run requests, applies the one-run guard, and starts the orchestrated retrieval flow.
using DataRetriever.Api.Contracts;
using DataRetriever.Application.Runs;
using DataRetriever.Execution;
using Microsoft.FeatureManagement;

namespace DataRetriever.Api.Endpoints;

public static class RunDataRetrievalEndpoint
{
    public static async Task<IResult> HandleAsync(
        RunDataRetrievalRequest? request,
        DataRetrievalOrchestrator orchestrator,
        SingleRunGuard singleRunGuard,
        IFeatureManager featureManager,
        CancellationToken cancellationToken)
    {
        if (!await featureManager.IsEnabledAsync(DataRetrieverFeatureFlags.RunRequests))
        {
            return Results.Json(
                new { message = "Data retrieval run requests are disabled." },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (!RunDataRetrievalRequestMapper.TryMap(request, out var options, out var errorMessage))
        {
            return Results.BadRequest(new
            {
                message = errorMessage
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

        var report = await orchestrator.RunAsync(options, cancellationToken);
        if (report.Status != RunStatus.Success)
        {
            return Results.Json(report, statusCode: StatusCodes.Status500InternalServerError);
        }

        return Results.Ok(report);
    }
}

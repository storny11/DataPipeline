using DataRetriever.Monitoring;

namespace DataRetriever.Api.Endpoints;

public static class GetDataRetrievalStatusEndpoint
{
    public static async Task<IResult> HandleAsync(
        IProcessingTracker processingTracker,
        CancellationToken cancellationToken)
    {
        var snapshot = await processingTracker.GetLatestSnapshotAsync(cancellationToken);
        return Results.Ok(snapshot);
    }
}

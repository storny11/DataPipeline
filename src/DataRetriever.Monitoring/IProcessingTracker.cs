// Defines how runs create and read operational progress snapshots.
namespace DataRetriever.Monitoring;

public interface IProcessingTracker
{
    IRunInstrumentation ForRun(Guid runId);

    Task<ProcessingRunSnapshot?> GetSnapshotAsync(
        Guid runId,
        CancellationToken cancellationToken);

    Task<ProcessingRunSnapshot?> GetLatestSnapshotAsync(CancellationToken cancellationToken);
}

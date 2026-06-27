using DataRetriever.Execution;

namespace DataRetriever.Monitoring;

public sealed record ProcessingRunSnapshot(
    Guid? RunId,
    RunStatus RunStatus,
    DateTimeOffset? LastAttemptedRunStartedAt,
    DateTimeOffset? LastAttemptedRunCompletedAt,
    DateTimeOffset? LastSuccessfulRunCompletedAt,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>> Values)
{
    public static ProcessingRunSnapshot NeverRun { get; } = new(
        null,
        RunStatus.NeverRun,
        null,
        null,
        null,
        new Dictionary<string, IReadOnlyDictionary<string, object?>>());
}

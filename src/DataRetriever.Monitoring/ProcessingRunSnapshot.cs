namespace DataRetriever.Monitoring;

public sealed record ProcessingRunSnapshot(
    Guid? RunId,
    ProcessingRunStatus RunStatus,
    DateTimeOffset? LastAttemptedRunStartedAt,
    DateTimeOffset? LastAttemptedRunCompletedAt,
    DateTimeOffset? LastSuccessfulRunCompletedAt,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>> Values)
{
    public static ProcessingRunSnapshot NeverRun { get; } = new(
        null,
        ProcessingRunStatus.NeverRun,
        null,
        null,
        null,
        new Dictionary<string, IReadOnlyDictionary<string, object?>>());
}

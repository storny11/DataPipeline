namespace DataRetriever.Execution;

public sealed record RunContext(
    Guid RunId,
    DateTimeOffset StartedAt);

// Carries run identity and timing through every step.
namespace DataRetriever.Execution;

public sealed record RunContext(
    Guid RunId,
    DateTimeOffset StartedAt);

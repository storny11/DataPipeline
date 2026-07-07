// Minimal outcome of a run returned by the API; the full report is published by RunReporting.
using DataRetriever.Execution;

namespace DataRetriever.Application.Runs;

public sealed record DataRetrievalRunResult(
    Guid RunId,
    RunStatus Status);

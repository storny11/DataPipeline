// Provides a non-generic view of step results for reporting and orchestration helpers.
namespace DataRetriever.Execution;

public interface IStepExecutionResult
{
    string StepName { get; }

    StepExecutionStatus Status { get; }

    bool HasUsableOutput { get; }

    IReadOnlyList<StepCounter> Counters { get; }

    IReadOnlyList<StepIssue> Issues { get; }

    bool HasErrors { get; }
}

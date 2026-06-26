using DataRetriever.Execution;

namespace DataRetriever.Tests.Execution;

public sealed class StepExecutionResultTests
{
    [Fact]
    public void Success_WithWarning_ReturnsSucceededWithIssues()
    {
        var result = StepExecutionResult<string>.Success(
            "Step",
            "output",
            issues:
            [
                new StepIssue(
                    "Step",
                    StepIssueSeverity.Warning,
                    "warning",
                    DiagnosticContext.From(("internalId", "INT-001")))
            ]);

        Assert.Equal(StepExecutionStatus.SucceededWithIssues, result.Status);
        Assert.True(result.HasUsableOutput);
        Assert.False(result.HasErrors);
    }

    [Fact]
    public void Failed_ReturnsNoUsableOutput()
    {
        var result = StepExecutionResult<string>.Failed(
            "Step",
            [
                new StepIssue(
                    "Step",
                    StepIssueSeverity.Error,
                    "error",
                    DiagnosticContext.From(("internalId", "INT-001")))
            ]);

        Assert.Equal(StepExecutionStatus.Failed, result.Status);
        Assert.False(result.HasUsableOutput);
        Assert.True(result.HasErrors);
    }
}

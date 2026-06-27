// Validates Step 1 configured rows before downstream steps consume them.
using DataRetriever.Application.Step1Load.Models;
using DataRetriever.Execution;

namespace DataRetriever.Application.Step1Load;

public sealed class Step1Validator
{
    public Step1ValidationResult Validate(IReadOnlyList<Step1Dto> rows)
    {
        var validRows = new List<Step1Dto>();
        var issues = new List<StepIssue>();

        foreach (var row in rows)
        {
            var rowIssues = ValidateRow(row);
            if (rowIssues.Count == 0)
            {
                validRows.Add(row);
            }
            else
            {
                issues.AddRange(rowIssues);
            }
        }

        return new Step1ValidationResult(validRows, issues);
    }

    private static IReadOnlyList<StepIssue> ValidateRow(Step1Dto row)
    {
        var issues = new List<StepIssue>();
        var context = DiagnosticContext.From(
            ("internalId", row.InternalId),
            ("externalId1", row.ExternalId1),
            ("currency", row.Currency));

        if (string.IsNullOrWhiteSpace(row.InternalId))
        {
            issues.Add(new StepIssue(
                Step1Loader.StepName,
                StepIssueSeverity.Warning,
                "Configured row is missing internal id.",
                context));
        }

        if (string.IsNullOrWhiteSpace(row.ExternalId1))
        {
            issues.Add(new StepIssue(
                Step1Loader.StepName,
                StepIssueSeverity.Warning,
                "Configured row is missing external id 1.",
                context));
        }

        if (string.IsNullOrWhiteSpace(row.Currency))
        {
            issues.Add(new StepIssue(
                Step1Loader.StepName,
                StepIssueSeverity.Warning,
                "Configured row is missing currency.",
                context));
        }

        if (row.Step2RecordsToKeep <= 0)
        {
            issues.Add(new StepIssue(
                Step1Loader.StepName,
                StepIssueSeverity.Warning,
                "Configured row has invalid Step 2 records-to-keep value.",
                context));
        }

        return issues;
    }
}

public sealed record Step1ValidationResult(
    IReadOnlyList<Step1Dto> ValidRows,
    IReadOnlyList<StepIssue> Issues);

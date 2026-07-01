// Validates Step 1 configured rows before downstream steps consume them.
using System.Globalization;
using DataRetriever.Application.Step1Load.Models;
using DataRetriever.Execution;

namespace DataRetriever.Application.Step1Load;

public sealed class Step1Validator
{
    public Step1ValidationResult Validate(IReadOnlyList<Step1SourceRow> rows)
    {
        var validRows = new List<Step1SourceRow>();
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

    private static IReadOnlyList<StepIssue> ValidateRow(Step1SourceRow row)
    {
        var issues = new List<StepIssue>();
        var context = DiagnosticContext.From(
            ("internalId", row.InternalId),
            ("externalId1", row.ExternalId1),
            ("currency", row.Currency),
            ("step2RecordsToKeep", row.Step2RecordsToKeep));

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

        if (!TryParsePositiveStep2RecordsToKeep(row.Step2RecordsToKeep, out _))
        {
            issues.Add(new StepIssue(
                Step1Loader.StepName,
                StepIssueSeverity.Warning,
                "Configured row has invalid Step 2 records-to-keep value.",
                context));
        }

        return issues;
    }

    private static bool TryParsePositiveStep2RecordsToKeep(string? value, out int recordsToKeep)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out recordsToKeep) &&
            recordsToKeep > 0;
    }
}

public sealed record Step1ValidationResult(
    IReadOnlyList<Step1SourceRow> ValidRows,
    IReadOnlyList<StepIssue> Issues);

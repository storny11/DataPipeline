// Validates Step 1 configured rows before downstream steps consume them, reporting issues at origin.
using System.Globalization;
using DataRetriever.Application.Step1Load.Models;
using RunReporting;

namespace DataRetriever.Application.Step1Load;

public sealed class Step1Validator(IRunReporter reporter)
{
    public IReadOnlyList<Step1SourceRow> Validate(IReadOnlyList<Step1SourceRow> rows)
    {
        return rows.Where(IsValid).ToList();
    }

    private bool IsValid(Step1SourceRow row)
    {
        var key = IssueKey(row);
        var data = IssueData.From(
            ("internalId", row.InternalId),
            ("externalId1", row.ExternalId1),
            ("currency", row.Currency),
            ("step2RecordsToKeep", row.Step2RecordsToKeep));

        var valid = true;

        if (string.IsNullOrWhiteSpace(row.InternalId))
        {
            reporter.AddIssue(Step1Loader.StepName, key, "Configured row is missing internal id.", data);
            valid = false;
        }

        if (string.IsNullOrWhiteSpace(row.ExternalId1))
        {
            reporter.AddIssue(Step1Loader.StepName, key, "Configured row is missing external id 1.", data);
            valid = false;
        }

        if (string.IsNullOrWhiteSpace(row.Currency))
        {
            reporter.AddIssue(Step1Loader.StepName, key, "Configured row is missing currency.", data);
            valid = false;
        }

        if (!TryParsePositiveStep2RecordsToKeep(row.Step2RecordsToKeep, out _))
        {
            reporter.AddIssue(Step1Loader.StepName, key, "Configured row has invalid Step 2 records-to-keep value.", data);
            valid = false;
        }

        return valid;
    }

    private static string IssueKey(Step1SourceRow row)
    {
        if (!string.IsNullOrWhiteSpace(row.InternalId))
        {
            return row.InternalId.Trim();
        }

        if (!string.IsNullOrWhiteSpace(row.ExternalId1))
        {
            return row.ExternalId1.Trim();
        }

        return $"configured-row:{row.Currency?.Trim()}:{row.Step2RecordsToKeep?.Trim()}";
    }

    private static bool TryParsePositiveStep2RecordsToKeep(string? value, out int recordsToKeep)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out recordsToKeep) &&
            recordsToKeep > 0;
    }
}

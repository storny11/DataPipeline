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
        var identifier = Identifier(row);

        var valid = true;

        if (string.IsNullOrWhiteSpace(row.InternalId))
        {
            reporter.AddIssue(Step1Loader.StepName, identifier.Name, identifier.Value, "Configured row is missing internal id.");
            valid = false;
        }

        if (string.IsNullOrWhiteSpace(row.ExternalId1))
        {
            reporter.AddIssue(Step1Loader.StepName, identifier.Name, identifier.Value, "Configured row is missing external id 1.");
            valid = false;
        }

        if (string.IsNullOrWhiteSpace(row.Currency))
        {
            reporter.AddIssue(Step1Loader.StepName, identifier.Name, identifier.Value, "Configured row is missing currency.");
            valid = false;
        }

        if (!TryParsePositiveStep2RecordsToKeep(row.Step2RecordsToKeep, out _))
        {
            reporter.AddIssue(Step1Loader.StepName, identifier.Name, identifier.Value, "Configured row has invalid Step 2 records-to-keep value.");
            valid = false;
        }

        return valid;
    }

    private static (string Name, string Value) Identifier(Step1SourceRow row)
    {
        if (!string.IsNullOrWhiteSpace(row.InternalId))
        {
            return ("InternalId", row.InternalId.Trim());
        }

        if (!string.IsNullOrWhiteSpace(row.ExternalId1))
        {
            return ("ExternalId1", row.ExternalId1.Trim());
        }

        return ("SourceRow", "unidentified");
    }

    private static bool TryParsePositiveStep2RecordsToKeep(string? value, out int recordsToKeep)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out recordsToKeep) &&
            recordsToKeep > 0;
    }
}

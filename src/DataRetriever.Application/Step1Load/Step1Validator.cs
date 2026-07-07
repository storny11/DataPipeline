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
        var subject = new
        {
            internalId = row.InternalId,
            externalId1 = row.ExternalId1,
            currency = row.Currency,
            step2RecordsToKeep = row.Step2RecordsToKeep
        };

        var valid = true;

        if (string.IsNullOrWhiteSpace(row.InternalId))
        {
            reporter.AddIssue(subject, "Configured row is missing internal id.", Step1Loader.StepName);
            valid = false;
        }

        if (string.IsNullOrWhiteSpace(row.ExternalId1))
        {
            reporter.AddIssue(subject, "Configured row is missing external id 1.", Step1Loader.StepName);
            valid = false;
        }

        if (string.IsNullOrWhiteSpace(row.Currency))
        {
            reporter.AddIssue(subject, "Configured row is missing currency.", Step1Loader.StepName);
            valid = false;
        }

        if (!TryParsePositiveStep2RecordsToKeep(row.Step2RecordsToKeep, out _))
        {
            reporter.AddIssue(subject, "Configured row has invalid Step 2 records-to-keep value.", Step1Loader.StepName);
            valid = false;
        }

        return valid;
    }

    private static bool TryParsePositiveStep2RecordsToKeep(string? value, out int recordsToKeep)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out recordsToKeep) &&
            recordsToKeep > 0;
    }
}

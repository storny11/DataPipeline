// Maps Step 3 source response rows into parsed amount values, reporting discards at origin.
using System.Globalization;
using DataRetriever.Application.Step3Load.Models;
using RunReporting;

namespace DataRetriever.Application.Step3Load;

public sealed class Step3ResponseMapper(ExternalId2Normalizer normalizer, IRunReporter reporter)
{
    public IReadOnlyDictionary<NormalizedExternalId2, Step3MappedAmounts> Map(
        IReadOnlyList<Step3ResponseItemDto> rows,
        IReadOnlyDictionary<NormalizedExternalId2, object> subjectByExternalId2)
    {
        var amounts = new Dictionary<NormalizedExternalId2, Step3MappedAmounts>();

        foreach (var row in rows)
        {
            if (!normalizer.TryNormalize(row.ExternalId2, out var normalized))
            {
                reporter.AddIssue(
                    new { externalId2 = row.ExternalId2 },
                    "Step 3 response row has missing or invalid external id 2 and was discarded.",
                    Step3Loader.StepName);
                continue;
            }

            if (!TryAmount(row.Amount1, out var amount1) ||
                !TryAmount(row.Amount2, out var amount2) ||
                !TryAmount(row.Amount3, out var amount3))
            {
                reporter.AddIssue(
                    Subject(subjectByExternalId2, normalized, row),
                    $"Step 3 response row for external id 2 '{row.ExternalId2}' has missing or invalid amount data and was discarded.",
                    Step3Loader.StepName);
                continue;
            }

            if (amounts.ContainsKey(normalized))
            {
                reporter.AddIssue(
                    Subject(subjectByExternalId2, normalized, row),
                    $"Step 3 response returned more than one valid row for external id 2 '{row.ExternalId2}'. The duplicate row was discarded and the first value was kept.",
                    Step3Loader.StepName);
                continue;
            }

            amounts.Add(normalized, new Step3MappedAmounts(normalized, amount1, amount2, amount3));
        }

        return amounts;
    }

    private static object Subject(
        IReadOnlyDictionary<NormalizedExternalId2, object> subjectByExternalId2,
        NormalizedExternalId2 normalized,
        Step3ResponseItemDto row)
    {
        return subjectByExternalId2.TryGetValue(normalized, out var subject)
            ? subject
            : new { externalId2 = row.ExternalId2 };
    }

    private static bool TryAmount(string? value, out decimal amount)
    {
        return decimal.TryParse(
            value,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out amount);
    }
}

public sealed record Step3MappedAmounts(
    NormalizedExternalId2 ExternalId2,
    decimal Amount1,
    decimal Amount2,
    decimal Amount3);

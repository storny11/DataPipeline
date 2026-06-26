using DataRetriever.Application.Step3Load.Models;
using DataRetriever.Execution;

namespace DataRetriever.Application.Step3Load;

public sealed class Step3ResponseMapper(ExternalId2Normalizer normalizer)
{
    public Step3ResponseMappingResult Map(
        IReadOnlyList<Step3ResponseItemDto> rows,
        IReadOnlyDictionary<NormalizedExternalId2, DiagnosticContext> contextByExternalId2)
    {
        var amounts = new Dictionary<NormalizedExternalId2, Step3MappedAmounts>();
        var issues = new List<StepIssue>();

        foreach (var row in rows)
        {
            if (!normalizer.TryNormalize(row.ExternalId2, out var normalized))
            {
                issues.Add(new StepIssue(
                    Step3Loader.StepName,
                    StepIssueSeverity.Warning,
                    "Step 3 response row has missing or invalid external id 2 and was discarded.",
                    DiagnosticContext.From(("externalId2", row.ExternalId2))));
                continue;
            }

            if (!TryAmount(row.Amount1, out var amount1) ||
                !TryAmount(row.Amount2, out var amount2) ||
                !TryAmount(row.Amount3, out var amount3))
            {
                var context = contextByExternalId2.TryGetValue(normalized, out var matchedContext)
                    ? matchedContext
                    : DiagnosticContext.From(("externalId2", row.ExternalId2));

                issues.Add(new StepIssue(
                    Step3Loader.StepName,
                    StepIssueSeverity.Warning,
                    $"Step 3 response row for external id 2 '{row.ExternalId2}' has missing or invalid amount data and was discarded.",
                    context));
                continue;
            }

            amounts[normalized] = new Step3MappedAmounts(normalized, amount1, amount2, amount3);
        }

        return new Step3ResponseMappingResult(amounts, issues);
    }

    private static bool TryAmount(string? value, out decimal amount)
    {
        return decimal.TryParse(value, out amount);
    }
}

public sealed record Step3MappedAmounts(
    NormalizedExternalId2 ExternalId2,
    decimal Amount1,
    decimal Amount2,
    decimal Amount3);

public sealed record Step3ResponseMappingResult(
    IReadOnlyDictionary<NormalizedExternalId2, Step3MappedAmounts> Amounts,
    IReadOnlyList<StepIssue> Issues);

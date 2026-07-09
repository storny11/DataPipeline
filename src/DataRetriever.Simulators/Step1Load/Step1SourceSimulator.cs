// Applies the run selection to configured Step 1 seed data for simulator runs.
using DataRetriever.Application.Runs;
using DataRetriever.Application.Step1Load;
using DataRetriever.Application.Step1Load.Models;

namespace DataRetriever.Simulators.Step1Load;

public sealed class Step1SourceSimulator(SimulatorSeedData seedData) : IStep1SourceClient
{
    public Task<IReadOnlyList<Step1SourceRow>> LoadConfiguredDataAsync(
        DataRetrievalRunOptions selection,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IEnumerable<Step1SourceRow> rows = seedData.ConfiguredRows;
        if (!string.IsNullOrWhiteSpace(selection.Currency))
        {
            rows = rows.Where(row => string.Equals(
                row.Currency?.Trim(),
                selection.Currency,
                StringComparison.OrdinalIgnoreCase));
        }
        else if (selection.InternalIds.Count > 0)
        {
            var ids = selection.InternalIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
            rows = rows.Where(row => row.InternalId is not null && ids.Contains(row.InternalId.Trim()));
        }

        return Task.FromResult<IReadOnlyList<Step1SourceRow>>(rows.ToList());
    }
}

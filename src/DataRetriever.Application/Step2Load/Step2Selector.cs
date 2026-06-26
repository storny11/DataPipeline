using DataRetriever.Application.Step2Load.Models;

namespace DataRetriever.Application.Step2Load;

public sealed class Step2Selector
{
    public IReadOnlyList<Step2OutputRecord> SelectLatest(
        IReadOnlyList<Step2OutputRecord> records,
        int recordsToKeep)
    {
        return records
            .OrderByDescending(record => record.EffectiveDate)
            .Take(recordsToKeep)
            .ToList();
    }
}

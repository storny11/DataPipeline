// Represents typed row-level persistence outcomes returned by the sink.
namespace DataRetriever.Application.Step4Persist.Models;

public sealed record Step4PersistResult(IReadOnlyList<Step4PersistRecordResult> Records)
{
    public static Step4PersistResult AllSucceeded(int recordCount)
    {
        return new Step4PersistResult(
            Enumerable.Range(0, recordCount)
                .Select(index => new Step4PersistRecordResult(index, true))
                .ToList());
    }
}

public sealed record Step4PersistRecordResult(
    int RequestIndex,
    bool Succeeded,
    string? Message = null);

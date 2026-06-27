// Carries successfully mapped Step 3 records forward to persistence.
namespace DataRetriever.Application.Step3Load.Models;

public sealed record Step3Output(IReadOnlyList<Step3OutputRecord> Records);

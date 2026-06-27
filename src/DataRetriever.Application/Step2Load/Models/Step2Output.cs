// Carries selected Step 2 records forward to Step 3.
namespace DataRetriever.Application.Step2Load.Models;

public sealed record Step2Output(IReadOnlyList<Step2OutputRecord> Records);

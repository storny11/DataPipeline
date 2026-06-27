// Carries Step 1 records forward to Step 2.
namespace DataRetriever.Application.Step1Load.Models;

public sealed record Step1Output(IReadOnlyList<Step1OutputRecord> Records);

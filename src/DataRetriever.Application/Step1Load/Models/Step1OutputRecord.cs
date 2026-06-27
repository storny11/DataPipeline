// Represents one validated configured item emitted by Step 1.
namespace DataRetriever.Application.Step1Load.Models;

public sealed record Step1OutputRecord(
    string InternalId,
    string ExternalId1,
    string Currency,
    int Step2RecordsToKeep);

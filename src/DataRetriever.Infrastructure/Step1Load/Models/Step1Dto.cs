// Represents the exact Step 1 configured-data storage shape used by the real source adapter.
namespace DataRetriever.Infrastructure.Step1Load.Models;

public sealed record Step1Dto(
    string? InternalId,
    string? ExternalId1,
    string? Currency,
    string? Step2RecordsToKeep);

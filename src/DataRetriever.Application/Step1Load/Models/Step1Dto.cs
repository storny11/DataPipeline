// Represents the external/source row shape loaded by Step 1.
namespace DataRetriever.Application.Step1Load.Models;

public sealed record Step1Dto(
    string? InternalId,
    string? ExternalId1,
    string? Currency,
    string? Step2RecordsToKeep);

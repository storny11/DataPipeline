// Application-owned raw Step 1 source row after infrastructure has adapted the storage shape.
namespace DataRetriever.Application.Step1Load.Models;

public sealed record Step1SourceRow(
    string? InternalId,
    string? ExternalId1,
    string? Currency,
    string? Step2RecordsToKeep);

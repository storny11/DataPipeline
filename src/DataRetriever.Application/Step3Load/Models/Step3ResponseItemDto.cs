// Represents one Step 3 source response row.
namespace DataRetriever.Application.Step3Load.Models;

public sealed record Step3ResponseItemDto(
    string? ExternalId2,
    string? Amount1,
    string? Amount2,
    string? Amount3);

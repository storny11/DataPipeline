// Represents one source response row fetched by Step 2.
namespace DataRetriever.Application.Step2Load.Models;

public sealed record Step2ResponseDto(
    string? ExternalId2,
    DateOnly EffectiveDate);

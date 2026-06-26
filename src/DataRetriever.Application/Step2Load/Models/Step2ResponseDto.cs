namespace DataRetriever.Application.Step2Load.Models;

public sealed record Step2ResponseDto(
    string? ExternalId2,
    DateOnly EffectiveDate);

namespace DataRetriever.Application.Step4Persist.Models;

public sealed record Step4RequestDto(
    string InternalId,
    string ExternalId1,
    string ExternalId2,
    decimal Amount1,
    decimal Amount2,
    decimal Amount3);

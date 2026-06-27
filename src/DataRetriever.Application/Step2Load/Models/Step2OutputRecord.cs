// Represents one selected related record emitted by Step 2.
namespace DataRetriever.Application.Step2Load.Models;

public sealed record Step2OutputRecord(
    string InternalId,
    string ExternalId1,
    string ExternalId2,
    DateOnly EffectiveDate);

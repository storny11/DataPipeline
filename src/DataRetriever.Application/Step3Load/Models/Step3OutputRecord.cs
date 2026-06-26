namespace DataRetriever.Application.Step3Load.Models;

public sealed record Step3OutputRecord(
    string InternalId,
    string ExternalId1,
    string ExternalId2,
    decimal Amount1,
    decimal Amount2,
    decimal Amount3);

namespace DataRetriever.Reporting;

public sealed record PersistedRecordSummary(
    string InternalId,
    string ExternalId1,
    string ExternalId2,
    decimal Amount1,
    decimal Amount2,
    decimal Amount3);

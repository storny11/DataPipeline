namespace DataRetriever.Reporting;

public sealed record RunReportEmail(
    string Subject,
    string HtmlBody,
    string TextBody);

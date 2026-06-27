// Carries the rendered email subject, HTML body, and plain-text body.
namespace DataRetriever.Reporting;

public sealed record RunReportEmail(
    string Subject,
    string HtmlBody,
    string TextBody);

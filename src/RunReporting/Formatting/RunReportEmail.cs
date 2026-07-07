// Carries the rendered email subject and HTML body.
namespace RunReporting;

public sealed record RunReportEmail(
    string Subject,
    string HtmlBody);

namespace DataRetriever.Infrastructure.Reporting;

public sealed record EmailMessage(
    string From,
    IReadOnlyList<string> To,
    IReadOnlyList<string> Cc,
    IReadOnlyList<string> Bcc,
    string Subject,
    string HtmlBody,
    string TextBody);

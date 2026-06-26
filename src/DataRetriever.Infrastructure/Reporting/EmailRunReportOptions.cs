namespace DataRetriever.Infrastructure.Reporting;

public sealed class EmailRunReportOptions
{
    public const string SectionName = "EmailReport";

    public bool Enabled { get; init; }

    public string Host { get; init; } = "";

    public int Port { get; init; } = 25;

    public MailKit.Security.SecureSocketOptions SocketOptions { get; init; } = MailKit.Security.SecureSocketOptions.Auto;

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    public string? UserName { get; init; }

    public string? Password { get; init; }

    public string From { get; init; } = "";

    public string[] To { get; init; } = [];

    public string[] Cc { get; init; } = [];

    public string[] Bcc { get; init; } = [];
}

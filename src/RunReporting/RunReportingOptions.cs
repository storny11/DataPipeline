// Configures SMTP delivery and report identity; defaults suit a local smtp4dev container.
using System.Net.Mail;

namespace RunReporting;

public sealed class RunReportingOptions
{
    /// <summary>Conventional configuration section consumed by AddRunReporting(IConfiguration).</summary>
    public const string SectionName = "EmailReport";

    /// <summary>When false, the built-in email publisher sends nothing; other registered publishers still run.</summary>
    public bool Enabled { get; set; } = true;

    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 25;

    public bool UseSsl { get; set; }

    public string? UserName { get; set; }

    public string? Password { get; set; }

    public string From { get; set; } = "pipeline@localhost";

    // Recipients are ';'-separated strings, not arrays: layered configuration merges arrays
    // index-by-index (an override can never shorten or clear one), while a scalar is replaced
    // wholesale by the last layer that sets it. "To": "" in an override clears the list.
    public string To { get; set; } = "";

    public string Cc { get; set; } = "";

    public string Bcc { get; set; } = "";

    /// <summary>Recipients that receive a compact variant of the report instead of the full one —
    /// minimal markup suited to clients that mangle rich HTML, e.g. Teams channel email addresses.</summary>
    public string CompactTo { get; set; } = "";

    /// <summary>Identifies the service in generated subjects, the report footer, and the compact heading.
    /// Generated subjects bracket this value, e.g. ServiceName "MyService" becomes "[MyService] Run failed (...)".</summary>
    public string ServiceName { get; set; } = "";

    /// <summary>Overrides the email subject; receives the published report (attributes, outcome, counts, tables).
    /// Returning null/empty or throwing falls back to the default subject.</summary>
    public Func<RunReport, string?>? SubjectBuilder { get; set; }

    /// <summary>Upper bound for a single SMTP send; an unresponsive server cannot stall the run past this.</summary>
    public TimeSpan SendTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>When false, publishing a run that collected no issues and no tables sends nothing.</summary>
    public bool SendWhenNoIssues { get; set; } = true;

    internal IReadOnlyList<string> ToRecipients => Split(To);

    internal IReadOnlyList<string> CcRecipients => Split(Cc);

    internal IReadOnlyList<string> BccRecipients => Split(Bcc);

    internal IReadOnlyList<string> CompactToRecipients => Split(CompactTo);

    /// <summary>Problems that would prevent email publishing; empty when Enabled is false or the configuration is valid.</summary>
    public IReadOnlyList<string> GetValidationErrors()
    {
        return Enabled ? FindEmailConfigurationProblems().ToList() : [];
    }

    private IEnumerable<string> FindEmailConfigurationProblems()
    {
        if (string.IsNullOrWhiteSpace(Host))
        {
            yield return "Host is required";
        }

        if (Port is <= 0 or > 65535)
        {
            yield return "Port must be between 1 and 65535";
        }

        if (string.IsNullOrWhiteSpace(From))
        {
            yield return "From is required";
        }
        else if (!MailAddress.TryCreate(From, out _))
        {
            yield return $"From '{From}' is not a valid email address";
        }

        var recipients = ToRecipients.Concat(CcRecipients).Concat(BccRecipients).Concat(CompactToRecipients).ToList();
        if (recipients.Count == 0)
        {
            yield return "at least one To/Cc/Bcc/CompactTo recipient is required";
        }

        foreach (var recipient in recipients.Where(recipient => !MailAddress.TryCreate(recipient, out _)))
        {
            yield return $"recipient '{recipient}' is not a valid email address";
        }

        if (SendTimeout <= TimeSpan.Zero)
        {
            yield return "SendTimeout must be positive";
        }
    }

    private static IReadOnlyList<string> Split(string? recipients)
    {
        return string.IsNullOrWhiteSpace(recipients)
            ? []
            : recipients.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}

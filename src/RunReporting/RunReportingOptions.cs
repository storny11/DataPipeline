// Configures SMTP delivery and report identity; defaults suit a local smtp4dev container.
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

    public IList<string> To { get; set; } = new List<string>();

    public IList<string> Cc { get; set; } = new List<string>();

    public IList<string> Bcc { get; set; } = new List<string>();

    /// <summary>Shown in the subject and heading to identify which service the report came from.</summary>
    public string ApplicationName { get; set; } = "Pipeline";

    public string SubjectPrefix { get; set; } = "[Run Report]";

    /// <summary>Upper bound for a single SMTP send; an unresponsive server cannot stall the run past this.</summary>
    public TimeSpan SendTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>When false, publishing a run that collected no issues and no tables sends nothing.</summary>
    public bool SendWhenNoIssues { get; set; } = true;
}

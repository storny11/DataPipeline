// Defines how a run report becomes email subject and HTML; replace to change the layout.
namespace RunReporting;

public interface IRunReportFormatter
{
    Task<RunReportEmail> FormatAsync(RunReport report, CancellationToken cancellationToken);

    /// <summary>Formats the compact variant sent to CompactTo recipients (e.g. Teams channel email addresses).</summary>
    Task<RunReportEmail> FormatCompactAsync(RunReport report, CancellationToken cancellationToken);
}

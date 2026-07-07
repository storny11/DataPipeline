// Defines how a run report becomes email subject, HTML, and text; replace to change the layout.
namespace RunReporting;

public interface IRunReportFormatter
{
    Task<RunReportEmail> FormatAsync(RunReport report, CancellationToken cancellationToken);
}

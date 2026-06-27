// Defines how a structured run report becomes email subject, HTML, and text.
namespace DataRetriever.Reporting;

public interface IRunReportEmailFormatter
{
    Task<RunReportEmail> FormatAsync(RunReport report, CancellationToken cancellationToken);
}

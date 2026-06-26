namespace DataRetriever.Reporting;

public interface IRunReportEmailFormatter
{
    Task<RunReportEmail> FormatAsync(RunReport report, CancellationToken cancellationToken);
}

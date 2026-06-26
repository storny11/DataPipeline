namespace DataRetriever.Reporting;

public interface IRunReportPublisher
{
    Task PublishAsync(RunReport report, CancellationToken cancellationToken);
}

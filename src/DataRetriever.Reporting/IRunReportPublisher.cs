// Defines the reporting publication boundary used by the application finalizer.
namespace DataRetriever.Reporting;

public interface IRunReportPublisher
{
    Task PublishAsync(RunReport report, CancellationToken cancellationToken);
}

// Delivery boundary for published reports; register any number (email, Teams, ...) and all receive each report.
namespace RunReporting;

public interface IRunReportPublisher
{
    Task PublishAsync(RunReport report, CancellationToken cancellationToken);
}

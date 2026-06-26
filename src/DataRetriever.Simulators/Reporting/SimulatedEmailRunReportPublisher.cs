using DataRetriever.Reporting;
using Microsoft.Extensions.Logging;

namespace DataRetriever.Simulators.Reporting;

public sealed class SimulatedEmailRunReportPublisher(
    ILogger<SimulatedEmailRunReportPublisher> logger) : IRunReportPublisher
{
    public Task PublishAsync(RunReport report, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Simulated report email for run {RunId}: {Status}, {WarningCount} warnings, {ErrorCount} errors, {PersistedCount} persisted rows",
            report.RunId,
            report.Status,
            report.Summary.WarningCount,
            report.Summary.ErrorCount,
            report.PersistedRecords.Count);

        return Task.CompletedTask;
    }
}

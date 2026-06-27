// Logs report publication instead of sending email when simulator reporting is used.
using DataRetriever.Reporting;
using Microsoft.Extensions.Logging;

namespace DataRetriever.Simulators.Reporting;

public sealed class SimulatedEmailRunReportPublisher(
    ILogger<SimulatedEmailRunReportPublisher> logger) : IRunReportPublisher
{
    public Task PublishAsync(RunReport report, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Simulated report email for run {RunId}: {Status}, {WarningCount} warnings, {ErrorCount} errors, {TableCount} tables, {TableRowCount} table rows",
            report.RunId,
            report.Status,
            report.WarningCount,
            report.ErrorCount,
            report.Tables.Count,
            report.Tables.Sum(table => table.Rows.Count));

        return Task.CompletedTask;
    }
}

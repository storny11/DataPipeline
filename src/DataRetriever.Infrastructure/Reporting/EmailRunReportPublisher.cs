using DataRetriever.Reporting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DataRetriever.Infrastructure.Reporting;

public sealed class EmailRunReportPublisher(
    IOptions<EmailRunReportOptions> options,
    ILogger<EmailRunReportPublisher> logger) : IRunReportPublisher
{
    public Task PublishAsync(RunReport report, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation(
                "Email publishing is disabled. Built report for run {RunId} with {IssueCount} issues.",
                report.RunId,
                report.Issues.Count);
            return Task.CompletedTask;
        }

        logger.LogInformation(
            "Email publishing placeholder for run {RunId}. Persisted rows: {PersistedCount}",
            report.RunId,
            report.PersistedRecords.Count);
        return Task.CompletedTask;
    }
}

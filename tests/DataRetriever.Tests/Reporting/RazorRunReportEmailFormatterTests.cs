using DataRetriever.Execution;
using DataRetriever.Reporting;
using Microsoft.Extensions.DependencyInjection;

namespace DataRetriever.Tests.Reporting;

public sealed class RazorRunReportEmailFormatterTests
{
    [Fact]
    public async Task FormatAsync_IncludesPersistedRecordsWarningsAndErrors()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataRetrieverReporting();

        await using var provider = services.BuildServiceProvider();
        var formatter = provider.GetRequiredService<IRunReportEmailFormatter>();

        var report = new RunReport(
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            DateTimeOffset.Parse("2026-06-26T09:00:00+00:00"),
            DateTimeOffset.Parse("2026-06-26T09:01:00+00:00"),
            RunStatus.Success,
            new RunRequestSummary("GBP", ["INT-1"]),
            new RunReportSummary(1, 1, 1, 1, 2, 1, 1),
            [],
            [
                new RunReportIssue(
                    "Step2Load",
                    StepIssueSeverity.Warning,
                    "No row returned for requested id",
                    DiagnosticContext.From(("internalId", "INT-1"), ("externalId1", "EXT-A"))),
                new RunReportIssue(
                    "Step3Load",
                    StepIssueSeverity.Error,
                    "Failed to parse amount",
                    DiagnosticContext.From(("internalId", "INT-2"), ("externalId2", "EXT-B")))
            ],
            [
                new PersistedRecordSummary("INT-1", "EXT-A", "EXT-B", 10.1m, 20.2m, 30.3m),
                new PersistedRecordSummary("INT-2", "EXT-C", "EXT-D", 11.1m, 21.2m, 31.3m)
            ]);

        var email = await formatter.FormatAsync(report, CancellationToken.None);

        Assert.Contains("Persisted Records", email.HtmlBody);
        Assert.Contains("INT-1", email.HtmlBody);
        Assert.Contains("INT-2", email.HtmlBody);
        Assert.Contains("No row returned for requested id", email.HtmlBody);
        Assert.Contains("Failed to parse amount", email.HtmlBody);
        Assert.Contains("InternalId=INT-1", email.TextBody);
        Assert.Contains("ExternalId1=EXT-C", email.TextBody);
        Assert.Contains("Warnings", email.TextBody);
        Assert.Contains("Errors", email.TextBody);
    }
}

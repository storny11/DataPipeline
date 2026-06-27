// Verifies report email layout, ordering, and generic table rendering.
using DataRetriever.Execution;
using DataRetriever.Reporting;
using Microsoft.Extensions.DependencyInjection;

namespace DataRetriever.Tests.Reporting;

public sealed class RazorRunReportEmailFormatterTests
{
    [Fact]
    public async Task FormatAsync_IncludesReportTablesWarningsAndErrors()
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
            [
                new RunReportMetric("rowsPersisted", "Rows persisted", "2"),
                new RunReportMetric("rowsRejected", "Rows rejected", "1")
            ],
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
                new RunReportTable(
                    "persisted-records",
                    "Persisted Records",
                    [
                        new RunReportColumn("internalId", "InternalId"),
                        new RunReportColumn("externalId1", "ExternalId1"),
                        new RunReportColumn("externalId2", "ExternalId2"),
                        new RunReportColumn("amount1", "Amount1", RunReportColumnAlignment.Right)
                    ],
                    [
                        new Dictionary<string, string?>
                        {
                            ["internalId"] = "INT-1",
                            ["externalId1"] = "EXT-A",
                            ["externalId2"] = "EXT-B",
                            ["amount1"] = "10.1000"
                        },
                        new Dictionary<string, string?>
                        {
                            ["internalId"] = "INT-2",
                            ["externalId1"] = "EXT-C",
                            ["externalId2"] = "EXT-D",
                            ["amount1"] = "11.1000"
                        }
                    ]),
                new RunReportTable(
                    "rejected-records",
                    "Rejected Records",
                    [new RunReportColumn("internalId", "InternalId")],
                    [new Dictionary<string, string?> { ["internalId"] = "INT-3" }])
            ]);

        var email = await formatter.FormatAsync(report, CancellationToken.None);

        Assert.Contains("Persisted Records", email.HtmlBody);
        Assert.Contains("Rejected Records", email.HtmlBody);
        Assert.Contains("INT-1", email.HtmlBody);
        Assert.Contains("INT-2", email.HtmlBody);
        Assert.Contains("INT-3", email.HtmlBody);
        Assert.Contains("No row returned for requested id", email.HtmlBody);
        Assert.Contains("Failed to parse amount", email.HtmlBody);
        Assert.Contains("InternalId=INT-1", email.TextBody);
        Assert.Contains("ExternalId1=EXT-C", email.TextBody);
        Assert.Contains("Warnings", email.TextBody);
        Assert.Contains("Errors", email.TextBody);
        Assert.True(email.TextBody.IndexOf("Errors", StringComparison.Ordinal) < email.TextBody.IndexOf("Warnings", StringComparison.Ordinal));
        Assert.True(email.TextBody.IndexOf("Warnings", StringComparison.Ordinal) < email.TextBody.IndexOf("Report tables", StringComparison.Ordinal));
        Assert.DoesNotContain("Rows persisted", email.HtmlBody);
    }

    [Fact]
    public async Task FormatAsync_WhenNoErrors_DoesNotRenderErrorsSection()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataRetrieverReporting();

        await using var provider = services.BuildServiceProvider();
        var formatter = provider.GetRequiredService<IRunReportEmailFormatter>();

        var report = new RunReport(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            DateTimeOffset.Parse("2026-06-26T09:00:00+00:00"),
            DateTimeOffset.Parse("2026-06-26T09:01:00+00:00"),
            RunStatus.Success,
            new RunRequestSummary(null, []),
            [],
            [],
            [
                new RunReportIssue(
                    "Step2Load",
                    StepIssueSeverity.Warning,
                    "No row returned for requested id",
                    DiagnosticContext.From(("internalId", "INT-1")))
            ],
            []);

        var email = await formatter.FormatAsync(report, CancellationToken.None);

        Assert.DoesNotContain("<h2 style=\"margin:24px 0 10px 0; font-size:18px; line-height:24px; color:#1d2939;\">Errors</h2>", email.HtmlBody);
        Assert.DoesNotContain("Errors", email.TextBody);
        Assert.Contains("Warnings", email.HtmlBody);
    }
}

// Verifies ambient run scoping, attribute flow, tables, and publish behavior of the RunReporting package.
using Microsoft.Extensions.DependencyInjection;
using RunReporting;

namespace DataRetriever.Tests.RunReporting;

public sealed class RunReporterTests
{
    [Fact]
    public void AddIssue_WithoutBeginRun_CollectsIntoDefaultRun()
    {
        var reporter = CreateReporter(out _);

        reporter.AddIssue(new { row = 7 }, "Invalid source row skipped.", "Step1Load");

        var report = reporter.Take();
        var issue = Assert.Single(report.Issues);
        Assert.Equal("Step1Load", issue.StepName);
        Assert.Equal(IssueSeverity.Warning, issue.Severity);
        Assert.Equal("7", issue.Data["row"]);
        Assert.Empty(report.Attributes);
        Assert.Empty(reporter.Take().Issues);
    }

    [Fact]
    public void BeginRun_NestsAndIsolatesRuns()
    {
        var reporter = CreateReporter(out _);

        using (reporter.BeginRun(("runId", "run-1")))
        {
            reporter.AddIssue("Outer issue.");

            using (reporter.BeginRun(("runId", "run-2")))
            {
                reporter.AddIssue("Inner issue.");

                var inner = reporter.Take();
                Assert.Equal("run-2", inner.Attributes["runId"]);
                Assert.Equal("Inner issue.", Assert.Single(inner.Issues).Message);
            }

            var outer = reporter.Take();
            Assert.Equal("run-1", outer.Attributes["runId"]);
            Assert.Equal("Outer issue.", Assert.Single(outer.Issues).Message);
        }
    }

    [Fact]
    public async Task BeginRun_FlowsAmbientlyAcrossAsyncCallsAndPublishesAttributes()
    {
        var reporter = CreateReporter(out var sender);

        using (reporter.BeginRun(("runId", "run-1"), ("environment", "test")))
        {
            reporter.AddIssue("Standalone message.");
            await AddFromNestedAsyncCall(reporter);
            await reporter.PublishAsync();
        }

        var report = Assert.Single(sender.Sent);
        Assert.Equal("run-1", report.Attributes["runId"]);
        Assert.Equal("test", report.Attributes["environment"]);
        Assert.Equal(2, report.Issues.Count);
    }

    [Fact]
    public async Task AddAttribute_AtAnyPointDuringTheRun_AppearsInPublishedReport()
    {
        var reporter = CreateReporter(out var sender);

        using (reporter.BeginRun(("runId", "run-1")))
        {
            reporter.AddIssue("Row skipped.");
            reporter.AddAttribute("environment", "prod");
            reporter.AddAttribute("RUNID", "run-2");
            await reporter.PublishAsync();
        }

        var report = Assert.Single(sender.Sent);
        Assert.Equal(2, report.Attributes.Count);
        Assert.Equal("prod", report.Attributes["environment"]);
        Assert.Equal("run-2", report.Attributes["runId"]);
    }

    [Fact]
    public void Take_DerivesOutcomeFromIssues()
    {
        var reporter = CreateReporter(out _);

        Assert.Equal(RunOutcome.Succeeded, reporter.Take().Outcome);

        reporter.AddIssue("Row skipped.");
        Assert.Equal(RunOutcome.CompletedWithWarnings, reporter.Take().Outcome);

        reporter.AddIssue("Lookup failed.", IssueSeverity.Error);
        Assert.Equal(RunOutcome.Failed, reporter.Take().Outcome);
    }

    [Fact]
    public async Task PublishAsync_WithExplicitOutcome_OverridesDerivedOneAndReturnsPublishedReport()
    {
        var reporter = CreateReporter(out var sender);

        reporter.AddIssue("Row skipped.");
        var published = await reporter.PublishAsync(RunOutcome.Failed);

        Assert.Same(published, Assert.Single(sender.Sent));
        Assert.Equal(RunOutcome.Failed, published.Outcome);
        Assert.Single(published.Issues);
    }

    [Fact]
    public async Task PublishAsync_FansOutToAllPublishers()
    {
        var first = new CapturingPublisher();
        var second = new CapturingPublisher();
        var reporter = new RunReporter(new RunReportingOptions(), [first, second]);

        reporter.AddIssue("Row skipped.");
        await reporter.PublishAsync();

        Assert.Single(first.Sent);
        Assert.Single(second.Sent);
    }

    [Fact]
    public void AddIssue_WithThrowingSubjectGetter_IsRecordedWithoutThrowing()
    {
        var reporter = CreateReporter(out _);

        reporter.AddIssue(new ExplosiveSubject(), "Row rejected.");

        var issue = Assert.Single(reporter.Take().Issues);
        Assert.Equal("INT-1", issue.Data["Id"]);
        Assert.Null(issue.Data["Bad"]);
    }

    [Fact]
    public void AddTable_WithNullArguments_IsToleratedWithoutThrowing()
    {
        var reporter = CreateReporter(out _);

        reporter.AddTable(null!, null!, null!);

        var table = Assert.Single(reporter.Take().Tables);
        Assert.Equal(string.Empty, table.Title);
        Assert.Empty(table.Rows);
    }

    [Fact]
    public void AddAttribute_WithEmptyName_IsIgnoredWithoutThrowing()
    {
        var reporter = CreateReporter(out _);

        reporter.AddAttribute("", "value");
        reporter.AddAttribute(null!, "value");

        Assert.Empty(reporter.Take().Attributes);
    }

    [Fact]
    public void Constructor_WithNullArguments_ProducesUsableReporter()
    {
        var reporter = new RunReporter(null, null);

        reporter.AddIssue("Still works.");

        Assert.Single(reporter.Take().Issues);
    }

    [Fact]
    public async Task PublishAsync_PublisherInternalTimeout_IsContainedAndOthersStillRun()
    {
        var healthy = new CapturingPublisher();
        var reporter = new RunReporter(new RunReportingOptions(), [new TimingOutPublisher(), healthy]);

        reporter.AddIssue("Row skipped.");
        await reporter.PublishAsync();

        Assert.Single(healthy.Sent);
    }

    [Fact]
    public async Task PublishAsync_WhenOnePublisherFails_DoesNotThrowAndStillReachesOthers()
    {
        var failing = new ThrowingPublisher();
        var healthy = new CapturingPublisher();
        var reporter = new RunReporter(new RunReportingOptions(), [failing, healthy]);

        reporter.AddIssue("Row skipped.");
        await reporter.PublishAsync();

        Assert.Single(healthy.Sent);
    }

    [Fact]
    public async Task PublishAsync_WithoutIssues_SkipsSendWhenConfiguredOff()
    {
        var sender = new CapturingPublisher();
        var reporter = new RunReporter(new RunReportingOptions { SendWhenNoIssues = false }, [sender]);

        await reporter.PublishAsync();

        Assert.Empty(sender.Sent);
    }

    [Fact]
    public async Task PublishAsync_WithOnlyTables_SendsEvenWhenNoIssueSendIsOff()
    {
        var sender = new CapturingPublisher();
        var reporter = new RunReporter(new RunReportingOptions { SendWhenNoIssues = false }, [sender]);

        reporter.AddTable("Persisted", ["id"], [new Dictionary<string, object?> { ["id"] = "INT-1" }]);
        await reporter.PublishAsync();

        Assert.Single(sender.Sent);
    }

    [Fact]
    public void AddIssue_WithScalarSubject_CapturesItAsId()
    {
        var reporter = CreateReporter(out _);
        var i = 2 + 3;

        reporter.AddIssue(i, "i should be lower than 1");

        var issue = Assert.Single(reporter.Take().Issues);
        Assert.Equal("5", issue.Data["id"]);
    }

    [Fact]
    public void AddIssue_WithListSubject_JoinsIdsIntoData()
    {
        var reporter = CreateReporter(out _);

        reporter.AddIssue(new[] { "GBP", "INT-1" }, "Combination rejected.");

        var issue = Assert.Single(reporter.Take().Issues);
        Assert.Equal("GBP, INT-1", issue.Data["ids"]);
    }

    [Fact]
    public async Task AddTable_PublishesDictionaryAndObjectRows()
    {
        var reporter = CreateReporter(out var sender);

        reporter.AddTable(
            "Fetched Rates",
            ["ccy", "rate"],
            [
                new Dictionary<string, object?> { ["ccy"] = "GBP", ["rate"] = 1.25m },
                new RateRow("EUR", 1.1m)
            ]);
        await reporter.PublishAsync();

        var table = Assert.Single(Assert.Single(sender.Sent).Tables);
        Assert.Equal("Fetched Rates", table.Title);
        Assert.Equal(["GBP", "1.25"], table.Rows[0]);
        Assert.Equal(["EUR", "1.1"], table.Rows[1]);
    }

    [Fact]
    public async Task RazorFormatter_RendersAttributesIssuesAndTablesEncoded()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRunReporting(options => options.ApplicationName = "TestApp");

        await using var provider = services.BuildServiceProvider();
        var formatter = provider.GetRequiredService<IRunReportFormatter>();

        var report = new RunReport(
            new Dictionary<string, string?> { ["runId"] = "run-1", ["environment"] = "test" },
            RunOutcome.Failed,
            DateTimeOffset.UtcNow,
            [RunIssue.Create("Step<1>", IssueSeverity.Error, "<script>alert(1)</script>")],
            [ResultTable.From("Persisted <Rows>", ["id"], [new Dictionary<string, object?> { ["id"] = "INT-1" }])]);

        var email = await formatter.FormatAsync(report, CancellationToken.None);

        Assert.Contains("TestApp run failed", email.Subject);
        Assert.DoesNotContain("<script>", email.HtmlBody);
        Assert.Contains("&lt;script&gt;", email.HtmlBody);
        Assert.Contains("Step&lt;1&gt;", email.HtmlBody);
        Assert.Contains("Persisted &lt;Rows&gt;", email.HtmlBody);
        Assert.Contains("Run failed", email.HtmlBody);
        Assert.Contains("run-1", email.HtmlBody);
        Assert.Contains("environment", email.HtmlBody);
        Assert.Contains("INT-1", email.HtmlBody);
    }

    private static async Task AddFromNestedAsyncCall(IRunReporter reporter)
    {
        await Task.Yield();
        reporter.AddIssue(42, "Subject message.");
    }

    private static RunReporter CreateReporter(out CapturingPublisher sender)
    {
        sender = new CapturingPublisher();
        return new RunReporter(new RunReportingOptions(), [sender]);
    }

    private sealed record RateRow(string Ccy, decimal Rate);

    private sealed class CapturingPublisher : IRunReportPublisher
    {
        public List<RunReport> Sent { get; } = [];

        public Task PublishAsync(RunReport report, CancellationToken cancellationToken)
        {
            Sent.Add(report);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingPublisher : IRunReportPublisher
    {
        public Task PublishAsync(RunReport report, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("SMTP unavailable.");
        }
    }

    // Simulates a publisher-internal timeout: cancellation NOT requested by the caller's token.
    private sealed class TimingOutPublisher : IRunReportPublisher
    {
        public Task PublishAsync(RunReport report, CancellationToken cancellationToken)
        {
            throw new OperationCanceledException("Send timed out.");
        }
    }

    private sealed class ExplosiveSubject
    {
        public string Id => "INT-1";

        public string Bad => throw new InvalidOperationException("Getter exploded.");
    }
}

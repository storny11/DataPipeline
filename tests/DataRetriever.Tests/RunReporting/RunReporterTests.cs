// Verifies ambient run scoping, attribute flow, tables, and publish behavior of the RunReporting package.
using Microsoft.Extensions.Configuration;
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
    public async Task Take_ResetsTheStartOfTheNextCollectionWindow()
    {
        var reporter = CreateReporter(out _);

        reporter.AddIssue("First cycle.");
        var first = reporter.Take();

        await Task.Delay(30);
        reporter.AddIssue("Second cycle.");
        var second = reporter.Take();

        Assert.True(second.StartedAtUtc > first.StartedAtUtc);
    }

    [Fact]
    public void BeginRun_WithCaseCollidingAndEmptyKeys_KeepsTheValidAttributes()
    {
        var reporter = CreateReporter(out _);

        var attributes = new Dictionary<string, string?>
        {
            ["Env"] = "a",
            ["env"] = "b",
            [""] = "ignored",
            ["runId"] = "run-1"
        };

        using (reporter.BeginRun(attributes))
        {
            var report = reporter.Take();
            Assert.Equal(2, report.Attributes.Count);
            Assert.Equal("run-1", report.Attributes["runId"]);
            Assert.True(report.Attributes.ContainsKey("env"));
        }
    }

    [Fact]
    public async Task RazorFormatter_StripsLineBreaksFromCustomSubject()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRunReporting(options =>
        {
            options.Enabled = false;
            options.SubjectBuilder = _ => "line1\r\nline2";
        });

        await using var provider = services.BuildServiceProvider();
        var formatter = provider.GetRequiredService<IRunReportFormatter>();

        var report = new RunReport(new Dictionary<string, string?>(), RunOutcome.Succeeded, DateTimeOffset.UtcNow, [], []);
        var email = await formatter.FormatAsync(report, CancellationToken.None);

        Assert.Equal("line1  line2", email.Subject);
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
    public void AddRunReporting_EnabledWithInvalidEmailConfiguration_FailsFastAtComposition()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddRunReporting(options => options.From = "not-an-address"));

        Assert.Contains("not-an-address", exception.Message);
        Assert.Contains("recipient", exception.Message);
    }

    [Fact]
    public async Task RazorFormatter_UsesCustomSubjectBuilder()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRunReporting(options =>
        {
            options.Enabled = false;
            options.SubjectBuilder = report =>
                $"MyService {report.Attributes["environment"]}: {report.Outcome} - {report.ErrorCount} errors, {report.WarningCount} warnings";
        });

        await using var provider = services.BuildServiceProvider();
        var formatter = provider.GetRequiredService<IRunReportFormatter>();

        var report = new RunReport(
            new Dictionary<string, string?> { ["environment"] = "prod" },
            RunOutcome.CompletedWithWarnings,
            DateTimeOffset.UtcNow,
            [RunIssue.Create("Step1", IssueSeverity.Warning, "Row skipped.")],
            []);

        var email = await formatter.FormatAsync(report, CancellationToken.None);

        Assert.Equal("MyService prod: CompletedWithWarnings - 0 errors, 1 warnings", email.Subject);
    }

    [Fact]
    public async Task RazorFormatter_WhenCustomSubjectBuilderThrows_FallsBackToDefaultSubject()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRunReporting(options =>
        {
            options.ServiceName = "TestApp";
            options.Enabled = false;
            options.SubjectBuilder = _ => throw new InvalidOperationException("Broken builder.");
        });

        await using var provider = services.BuildServiceProvider();
        var formatter = provider.GetRequiredService<IRunReportFormatter>();

        var report = new RunReport(
            new Dictionary<string, string?>(),
            RunOutcome.Succeeded,
            DateTimeOffset.UtcNow,
            [],
            []);

        var email = await formatter.FormatAsync(report, CancellationToken.None);

        Assert.Contains("[TestApp] Run succeeded", email.Subject);
    }

    [Fact]
    public async Task RazorFormatter_WithoutServiceName_OmitsSubjectPrefix()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRunReporting(options => options.Enabled = false);

        await using var provider = services.BuildServiceProvider();
        var formatter = provider.GetRequiredService<IRunReportFormatter>();

        var report = new RunReport(
            new Dictionary<string, string?>(),
            RunOutcome.Failed,
            DateTimeOffset.UtcNow,
            [RunIssue.Create("Step1", IssueSeverity.Error, "Boom.")],
            []);

        var email = await formatter.FormatAsync(report, CancellationToken.None);

        Assert.Equal("Run failed (1 errors, 0 warnings)", email.Subject);
    }

    [Fact]
    public async Task RazorFormatter_MetadataRendersOnlySuppliedAttributes()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRunReporting(options => options.Enabled = false);

        await using var provider = services.BuildServiceProvider();
        var formatter = provider.GetRequiredService<IRunReportFormatter>();

        var report = new RunReport(
            new Dictionary<string, string?>
            {
                ["runId"] = "run-1",
                ["started (ET)"] = "2026-07-08 10:08:00 -04:00",
                ["completed (ET)"] = "2026-07-08 10:15:00 -04:00"
            },
            RunOutcome.Succeeded,
            DateTimeOffset.UtcNow,
            [],
            []);

        var email = await formatter.FormatAsync(report, CancellationToken.None);

        Assert.Contains("RUN ID", email.HtmlBody);
        Assert.Contains("run-1", email.HtmlBody);
        Assert.Contains("STARTED (ET)", email.HtmlBody);
        Assert.Contains("2026-07-08 10:08:00 -04:00", email.HtmlBody);
        Assert.Contains("COMPLETED (ET)", email.HtmlBody);
        Assert.Contains("2026-07-08 10:15:00 -04:00", email.HtmlBody);
        Assert.DoesNotContain("DURATION", email.HtmlBody);
        Assert.Contains("border-left:4px solid #12b76a", email.HtmlBody);
        Assert.Contains("padding:14px 18px", email.HtmlBody);
        Assert.Contains("border:1px solid #e6ebf2", email.HtmlBody);
        Assert.Contains("background-color:#fbfcfe", email.HtmlBody);
    }

    [Fact]
    public async Task RazorFormatter_WarningOutcome_UsesShortWarningBadge()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRunReporting(options => options.Enabled = false);

        await using var provider = services.BuildServiceProvider();
        var formatter = provider.GetRequiredService<IRunReportFormatter>();

        var report = new RunReport(
            new Dictionary<string, string?>(),
            RunOutcome.CompletedWithWarnings,
            DateTimeOffset.UtcNow,
            [RunIssue.Create("Step1", IssueSeverity.Warning, "Review this row.")],
            []);

        var email = await formatter.FormatAsync(report, CancellationToken.None);

        Assert.Contains("Run completed with 1 warnings", email.HtmlBody);
        Assert.Contains(">Warnings</td>", email.HtmlBody);
        Assert.DoesNotContain(">Completed with warnings</td>", email.HtmlBody);
    }

    [Fact]
    public async Task RazorFormatter_CompactVariant_RendersSummaryTopIssuesAndTableCounts()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRunReporting(options =>
        {
            options.ServiceName = "TestApp";
            options.Enabled = false;
        });

        await using var provider = services.BuildServiceProvider();
        var formatter = provider.GetRequiredService<IRunReportFormatter>();

        var issues = Enumerable.Range(1, 12)
            .Select(index => RunIssue.Create("Step1", IssueSeverity.Warning, $"Row {index} skipped <b>."))
            .ToList();
        var report = new RunReport(
            new Dictionary<string, string?> { ["runId"] = "run-1" },
            RunOutcome.CompletedWithWarnings,
            DateTimeOffset.UtcNow,
            issues,
            [ResultTable.From("Persisted Records", ["ID"], [new { id = "INT-1" }])]);

        var email = await formatter.FormatCompactAsync(report, CancellationToken.None);

        Assert.Contains("TestApp", email.HtmlBody);
        Assert.Contains("Run completed with 12 warnings", email.HtmlBody);
        Assert.Contains("runId: run-1", email.HtmlBody);
        Assert.Contains("Row 1 skipped", email.HtmlBody);
        Assert.Contains("and 2 more issues", email.HtmlBody);
        Assert.Contains("Persisted Records (1)", email.HtmlBody);
        Assert.DoesNotContain("<b>", email.HtmlBody);
        Assert.Contains("[TestApp] Run completed with 12 warnings", email.Subject);
    }

    [Fact]
    public void AddRunReporting_CompactRecipientsAlone_SatisfyTheRecipientRequirement()
    {
        var services = new ServiceCollection();

        services.AddRunReporting(options => options.CompactTo = "channel@apac.teams.ms");

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<IRunReporter>());
    }

    [Fact]
    public void GetValidationErrors_ValidatesEachRecipientAndIgnoresEmptyEntries()
    {
        var options = new RunReportingOptions
        {
            To = "good@test.local; bad-address; ; another@test.local"
        };

        var error = Assert.Single(options.GetValidationErrors());
        Assert.Contains("bad-address", error);
    }

    [Fact]
    public void Recipients_OverrideLayerReplacesWholesale()
    {
        // Recipients are a scalar precisely so a later configuration layer replaces the whole list.
        var configuration = new ConfigurationBuilder()
            .AddJsonStream(Json("""{"EmailReport":{"Enabled":true,"To":"a@test.local;b@test.local"}}"""))
            .AddJsonStream(Json("""{"EmailReport":{"To":"c@test.local"}}"""))
            .Build();
        var services = new ServiceCollection();

        services.AddRunReporting(configuration);

        using var provider = services.BuildServiceProvider();
        Assert.Equal("c@test.local", provider.GetRequiredService<RunReportingOptions>().To);
    }

    private static MemoryStream Json(string json) => new(System.Text.Encoding.UTF8.GetBytes(json));

    [Fact]
    public async Task AddRunReporting_WithEmailDisabled_RequiresNoEmailConfiguration()
    {
        var services = new ServiceCollection();
        services.AddRunReporting(options => options.Enabled = false);

        await using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IRunReporter>());
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
    public void AddTable_WithThrowingFormatString_FallsBackToUnformattedValue()
    {
        var reporter = CreateReporter(out _);

        // "Z" is an invalid standard numeric specifier and makes decimal.ToString throw.
        reporter.AddTable("Rates", ["RATE"], [new { rate = 1.25m }], [Column.Number("Z")]);

        var table = Assert.Single(reporter.Take().Tables);
        Assert.Equal("1.25", Assert.Single(table.Rows)[0]);
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

        reporter.AddTable("Persisted", ["ID"], [new { id = "INT-1" }]);
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
    public async Task AddTable_PublishesAnonymousAndTypedRows()
    {
        var reporter = CreateReporter(out var sender);

        // Headers are verbatim; each matches a property ignoring case ("CCY" reads ccy or Ccy).
        reporter.AddTable(
            "Fetched Rates",
            ["CCY", "RATE"],
            [
                new { ccy = "GBP", rate = 1.25m },
                new RateRow("EUR", 1.1m)
            ],
            [Column.Left, Column.Number("N2")]);
        await reporter.PublishAsync();

        var table = Assert.Single(Assert.Single(sender.Sent).Tables);
        Assert.Equal("Fetched Rates", table.Title);
        Assert.Equal(["CCY", "RATE"], table.Headers);
        Assert.Equal(["GBP", "1.25"], table.Rows[0]);
        Assert.Equal(["EUR", "1.10"], table.Rows[1]);
        Assert.Equal([ColumnAlignment.Left, ColumnAlignment.Right], table.Alignments);
    }

    [Fact]
    public async Task RazorFormatter_RendersAttributesIssuesAndTablesEncoded()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRunReporting(options =>
        {
            options.ServiceName = "TestApp";
            options.Enabled = false;
        });

        await using var provider = services.BuildServiceProvider();
        var formatter = provider.GetRequiredService<IRunReportFormatter>();

        var report = new RunReport(
            new Dictionary<string, string?> { ["runId"] = "run-1", ["environment"] = "test" },
            RunOutcome.Failed,
            DateTimeOffset.UtcNow,
            [
                RunIssue.Create("Step<1>", IssueSeverity.Error, "<script>alert(1)</script>"),
                RunIssue.Create("Step<2>", IssueSeverity.Warning, "Review this row.")
            ],
            [ResultTable.From("Persisted <Rows>", ["ID"], [new { id = "INT-1" }])]);

        var email = await formatter.FormatAsync(report, CancellationToken.None);

        Assert.Contains("[TestApp] Run failed", email.Subject);
        Assert.DoesNotContain("<script>", email.HtmlBody);
        Assert.Contains("&lt;script&gt;", email.HtmlBody);
        Assert.Contains("Step&lt;1&gt;", email.HtmlBody);
        Assert.Contains("Persisted &lt;Rows&gt; (1)", email.HtmlBody);
        Assert.Contains("Run failed", email.HtmlBody);
        Assert.Contains("run-1", email.HtmlBody);
        Assert.Contains("font-family:Consolas", email.HtmlBody);
        Assert.Contains("word-wrap:break-word", email.HtmlBody);
        Assert.DoesNotContain("word-break:break-all", email.HtmlBody);
        Assert.DoesNotContain("opacity:0", email.HtmlBody);
        Assert.DoesNotContain("overflow:hidden", email.HtmlBody);
        Assert.DoesNotContain("visibility:hidden", email.HtmlBody);
        Assert.Contains("height:3px", email.HtmlBody);
        Assert.Contains("ENVIRONMENT", email.HtmlBody);
        Assert.Contains("border-left:4px solid #d92d20", email.HtmlBody);
        Assert.Contains("border-left:4px solid #f79009", email.HtmlBody);
        Assert.Contains("border-left:4px solid #2557a7", email.HtmlBody);
        Assert.Contains("font-size:19px; line-height:26px; color:#b42318; font-weight:700", email.HtmlBody);
        Assert.Contains("font-size:19px; line-height:26px; color:#92620a; font-weight:700", email.HtmlBody);
        Assert.Contains("border:1px solid #b42318", email.HtmlBody);
        Assert.Contains("background-color:#fffafa", email.HtmlBody);
        Assert.Contains("background-color:#fffdf7", email.HtmlBody);
        Assert.Contains("background-color:#fef3f2", email.HtmlBody);
        Assert.Contains("background-color:#fffaeb", email.HtmlBody);
        Assert.Contains("background-color:#2557a7", email.HtmlBody);
        Assert.Contains("padding:9px 12px; font-size:14px", email.HtmlBody);
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

// Verifies scoped run collection, completion, formatting, and publishing behavior.
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RunReporting;

namespace DataRetriever.Tests.RunReporting;

public sealed class RunReporterTests
{
    [Fact]
    public async Task AddIssue_CollectsIntoScopedRun()
    {
        var reporter = CreateReporter(out _);

        reporter.AddIssue(
            "Step1Load",
            "Row",
            "row-7",
            "Invalid source row skipped.");

        var report = await reporter.CompleteAsync();
        var issue = Assert.Single(report.Issues);
        Assert.Equal("Step1Load", issue.StepName);
        Assert.Equal("Row", issue.IdentifierName);
        Assert.Equal("row-7", issue.IdentifierValue);
        Assert.Equal(IssueSeverity.Warning, issue.Severity);
        Assert.Empty(report.Attributes);
        Assert.Same(report, await reporter.CompleteAsync());
    }

    [Fact]
    public async Task CompleteAsync_PublishesOnceAndIgnoresLaterWrites()
    {
        var reporter = CreateReporter(out var sender);
        reporter.AddAttribute("runId", "run-1");
        reporter.AddIssue("Before completion.");

        var completions = await Task.WhenAll(
            reporter.CompleteAsync(),
            reporter.CompleteAsync(RunOutcome.Failed));
        var first = completions[0];
        reporter.AddAttribute("environment", "ignored");
        reporter.AddIssue("After completion.");
        reporter.AddTable("Ignored", [new IdRow("INT-1")], [TableColumn<IdRow>.Left("ID", row => row.Id)]);
        var second = completions[1];

        Assert.Same(first, second);
        Assert.Same(first, Assert.Single(sender.Sent));
        Assert.Equal(RunOutcome.CompletedWithWarnings, first.Outcome);
        Assert.Equal("Before completion.", Assert.Single(first.Issues).Message);
        Assert.Empty(first.Tables);
        Assert.False(first.Attributes.ContainsKey("environment"));
    }

    [Fact]
    public async Task ScopedReporter_CollectsAcrossAsyncCallsAndPublishesAttributes()
    {
        var reporter = CreateReporter(out var sender);

        reporter.AddAttribute("runId", "run-1");
        reporter.AddAttribute("environment", "test");
        reporter.AddIssue("Standalone message.");
        await AddFromNestedAsyncCall(reporter);
        await reporter.CompleteAsync();

        var report = Assert.Single(sender.Sent);
        Assert.Equal("run-1", report.Attributes["runId"]);
        Assert.Equal("test", report.Attributes["environment"]);
        Assert.Equal(2, report.Issues.Count);
    }

    [Fact]
    public async Task AddAttribute_AtAnyPointDuringTheRun_AppearsInPublishedReport()
    {
        var reporter = CreateReporter(out var sender);

        reporter.AddAttribute("runId", "run-1");
        reporter.AddIssue("Row skipped.");
        reporter.AddAttribute("environment", "prod");
        reporter.AddAttribute("RUNID", "run-2");
        await reporter.CompleteAsync();

        var report = Assert.Single(sender.Sent);
        Assert.Equal(2, report.Attributes.Count);
        Assert.Equal("prod", report.Attributes["environment"]);
        Assert.Equal("run-2", report.Attributes["runId"]);
    }

    [Fact]
    public async Task AddAttribute_WithCaseCollidingAndEmptyNames_KeepsTheValidAttributes()
    {
        var reporter = CreateReporter(out _);

        reporter.AddAttribute("Env", "a");
        reporter.AddAttribute("env", "b");
        reporter.AddAttribute("", "ignored");
        reporter.AddAttribute("runId", "run-1");

        var report = await reporter.CompleteAsync();
        Assert.Equal(2, report.Attributes.Count);
        Assert.Equal("b", report.Attributes["env"]);
        Assert.Equal("run-1", report.Attributes["runId"]);
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
    public async Task CompleteAsync_DerivesOutcomeFromIssues()
    {
        var clean = CreateReporter(out _);
        Assert.Equal(RunOutcome.Succeeded, (await clean.CompleteAsync()).Outcome);

        var warning = CreateReporter(out _);
        warning.AddIssue("Row skipped.");
        Assert.Equal(RunOutcome.CompletedWithWarnings, (await warning.CompleteAsync()).Outcome);

        var failed = CreateReporter(out _);
        failed.AddIssue("Lookup failed.", IssueSeverity.Error);
        Assert.Equal(RunOutcome.Failed, (await failed.CompleteAsync()).Outcome);
    }

    [Fact]
    public async Task AddIssue_WithUnknownSeverity_TreatsItAsAWarning()
    {
        var reporter = CreateReporter(out _);

        reporter.AddIssue("Unknown severity.", (IssueSeverity)999);

        var report = await reporter.CompleteAsync();
        Assert.Equal(RunOutcome.CompletedWithWarnings, report.Outcome);
        Assert.Equal(IssueSeverity.Warning, Assert.Single(report.Issues).Severity);
    }

    [Fact]
    public async Task CompleteAsync_WithExplicitOutcome_OverridesDerivedOneAndReturnsPublishedReport()
    {
        var reporter = CreateReporter(out var sender);

        reporter.AddIssue("Row skipped.");
        var published = await reporter.CompleteAsync(RunOutcome.Failed);

        Assert.Same(published, Assert.Single(sender.Sent));
        Assert.Equal(RunOutcome.Failed, published.Outcome);
        Assert.Single(published.Issues);
    }

    [Fact]
    public async Task CompleteAsync_FansOutToAllPublishers()
    {
        var first = new CapturingPublisher();
        var second = new CapturingPublisher();
        var reporter = new RunReporter(
            new RunReportingOptions(),
            [first, second],
            NullLogger<RunReporter>.Instance);

        reporter.AddIssue("Row skipped.");
        await reporter.CompleteAsync();

        Assert.Single(first.Sent);
        Assert.Single(second.Sent);
    }

    [Fact]
    public void AddSmtpRunReportPublisher_EnabledWithInvalidEmailConfiguration_FailsFastAtComposition()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddSmtpRunReportPublisher(options => options.From = "not-an-address"));

        Assert.Contains("not-an-address", exception.Message);
        Assert.Contains("recipient", exception.Message);
    }

    [Fact]
    public void AddRunReporting_ReplacesPreRegisteredOptionsWithConfiguredOptions()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new RunReportingOptions
        {
            Enabled = false,
            To = "old@test.local",
            ServiceName = "OldService"
        });

        services.AddRunReporting(options =>
        {
            options.To = "new@test.local";
            options.ServiceName = "NewService";
        });

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<RunReportingOptions>();
        Assert.Equal("new@test.local", options.To);
        Assert.Equal("NewService", options.ServiceName);
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
            [new RunIssue("Step1", "Row", "row-1", IssueSeverity.Warning, "Row skipped.")],
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
            [new RunIssue("Step1", "Row", "row-1", IssueSeverity.Error, "Boom.")],
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
            [new RunIssue("Step1", "Row", "row-1", IssueSeverity.Warning, "Review this row.")],
            []);

        var email = await formatter.FormatAsync(report, CancellationToken.None);

        Assert.Contains("Run completed with 1 warnings", email.HtmlBody);
        Assert.Contains(">Warnings</td>", email.HtmlBody);
        Assert.DoesNotContain(">Completed with warnings</td>", email.HtmlBody);
    }

    [Fact]
    public async Task RazorFormatter_CapsRenderedIssuesAtThirtyWithMoreNote()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRunReporting(options => options.Enabled = false);

        await using var provider = services.BuildServiceProvider();
        var formatter = provider.GetRequiredService<IRunReportFormatter>();

        var issues = Enumerable.Range(1, 35)
            .Select(index => new RunIssue(
                "Step1",
                "Row",
                $"row-{index:D3}",
                IssueSeverity.Warning,
                $"Issue message {index:D3}."))
            .ToList();
        var report = new RunReport(
            new Dictionary<string, string?>(),
            RunOutcome.CompletedWithWarnings,
            DateTimeOffset.UtcNow,
            issues,
            []);

        var email = await formatter.FormatAsync(report, CancellationToken.None);

        Assert.Contains("Warnings (35)", email.HtmlBody);
        Assert.Contains("Issue message 030.", email.HtmlBody);
        Assert.DoesNotContain("Issue message 031.", email.HtmlBody);
        Assert.Contains("and 5 more not shown.", email.HtmlBody);
    }

    [Fact]
    public async Task RazorFormatter_CapsRenderedTableRowsAtThirtyWithMoreNote()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRunReporting(options => options.Enabled = false);

        await using var provider = services.BuildServiceProvider();
        var formatter = provider.GetRequiredService<IRunReportFormatter>();

        var rows = Enumerable.Range(1, 35)
            .Select(index => (IReadOnlyList<string?>)[$"cell-{index:D3}"])
            .ToList();
        var report = new RunReport(
            new Dictionary<string, string?>(),
            RunOutcome.Succeeded,
            DateTimeOffset.UtcNow,
            [],
            [new ResultTable("Persisted Records", ["ID"], [ColumnAlignment.Left], rows)]);

        var email = await formatter.FormatAsync(report, CancellationToken.None);

        Assert.Contains("Persisted Records (35)", email.HtmlBody);
        Assert.Contains("cell-030", email.HtmlBody);
        Assert.DoesNotContain("cell-031", email.HtmlBody);
        Assert.Contains("and 5 more not shown.", email.HtmlBody);
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
            .Select(index => new RunIssue(
                "Step1",
                "Row",
                $"row-{index}",
                IssueSeverity.Warning,
                $"Row {index} skipped <b>."))
            .ToList();
        var report = new RunReport(
            new Dictionary<string, string?> { ["runId"] = "run-1" },
            RunOutcome.CompletedWithWarnings,
            DateTimeOffset.UtcNow,
            issues,
            [new ResultTable(
                "Persisted Records",
                ["ID"],
                [ColumnAlignment.Left],
                [["INT-1"]])]);

        var email = await formatter.FormatCompactAsync(report, CancellationToken.None);

        Assert.Contains("TestApp", email.HtmlBody);
        Assert.Contains("Run completed with 12 warnings", email.HtmlBody);
        Assert.Contains("Step1 / Row=row-1", email.HtmlBody);
        Assert.Contains("runId: run-1", email.HtmlBody);
        Assert.Contains("Row 1 skipped", email.HtmlBody);
        Assert.Contains("and 2 more issues", email.HtmlBody);
        Assert.Contains("Persisted Records (1)", email.HtmlBody);
        Assert.DoesNotContain("<b>", email.HtmlBody);
        Assert.Contains("[TestApp] Run completed with 12 warnings", email.Subject);
    }

    [Fact]
    public void AddRunReporting_DoesNotRegisterTheSmtpPublisher()
    {
        var services = new ServiceCollection();
        services.AddRunReporting();

        using var provider = services.BuildServiceProvider();

        Assert.Empty(provider.GetServices<IRunReportPublisher>());
    }

    [Fact]
    public void AddSmtpRunReportPublisher_CompactRecipientsAlone_SatisfyTheRecipientRequirement()
    {
        var services = new ServiceCollection();

        services.AddSmtpRunReportPublisher(options => options.CompactTo = "channel@apac.teams.ms");

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<IRunReporter>());
        Assert.IsType<EmailRunReportPublisher>(Assert.Single(provider.GetServices<IRunReportPublisher>()));
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

        services.AddSmtpRunReportPublisher(configuration);

        using var provider = services.BuildServiceProvider();
        Assert.Equal("c@test.local", provider.GetRequiredService<RunReportingOptions>().To);
    }

    private static MemoryStream Json(string json) => new(System.Text.Encoding.UTF8.GetBytes(json));

    [Fact]
    public async Task AddSmtpRunReportPublisher_WithEmailDisabled_RequiresNoEmailConfiguration()
    {
        var services = new ServiceCollection();
        services.AddSmtpRunReportPublisher(options => options.Enabled = false);

        await using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IRunReporter>());
    }

    [Fact]
    public async Task AddRunReporting_ReporterIsScopedAndIsolatesRuns()
    {
        var services = new ServiceCollection();
        services.AddRunReporting(options => options.Enabled = false);

        await using var provider = services.BuildServiceProvider();
        await using var firstScope = provider.CreateAsyncScope();
        await using var secondScope = provider.CreateAsyncScope();
        var first = firstScope.ServiceProvider.GetRequiredService<IRunReporter>();
        var second = secondScope.ServiceProvider.GetRequiredService<IRunReporter>();

        Assert.Same(first, firstScope.ServiceProvider.GetRequiredService<IRunReporter>());
        Assert.NotSame(first, second);

        first.AddAttribute("runId", "first");
        second.AddAttribute("runId", "second");

        Assert.Equal("first", (await first.CompleteAsync()).Attributes["runId"]);
        Assert.Equal("second", (await second.CompleteAsync()).Attributes["runId"]);
    }

    [Fact]
    public async Task AddTable_WithThrowingFormatString_FallsBackToUnformattedValue()
    {
        var reporter = CreateReporter(out _);

        // "Z" is an invalid standard numeric specifier and makes decimal.ToString throw.
        reporter.AddTable(
            "Rates",
            [new RateRow("GBP", 1.25m)],
            [TableColumn<RateRow>.Number("RATE", row => row.Rate, "Z")]);

        var table = Assert.Single((await reporter.CompleteAsync()).Tables);
        Assert.Equal("1.25", Assert.Single(table.Rows)[0]);
    }

    [Fact]
    public async Task AddTable_WithThrowingSelector_LosesOnlyThatCell()
    {
        var reporter = CreateReporter(out _);

        reporter.AddTable(
            "Rows",
            [new ExplosiveSubject()],
            [
                TableColumn<ExplosiveSubject>.Left("ID", row => row.Id),
                TableColumn<ExplosiveSubject>.Left("BAD", row => row.Bad)
            ]);

        var row = Assert.Single(Assert.Single((await reporter.CompleteAsync()).Tables).Rows);
        Assert.Equal("INT-1", row[0]);
        Assert.Null(row[1]);
    }

    [Fact]
    public async Task AddTable_WithNullArguments_IsToleratedWithoutThrowing()
    {
        var reporter = CreateReporter(out _);

        reporter.AddTable<object?>(null!, null!, null!);

        var table = Assert.Single((await reporter.CompleteAsync()).Tables);
        Assert.Equal(string.Empty, table.Title);
        Assert.Empty(table.Rows);
    }

    [Fact]
    public async Task AddAttribute_WithEmptyName_IsIgnoredWithoutThrowing()
    {
        var reporter = CreateReporter(out _);

        reporter.AddAttribute("", "value");
        reporter.AddAttribute(null!, "value");

        Assert.Empty((await reporter.CompleteAsync()).Attributes);
    }

    [Fact]
    public void Constructor_WithNullDependencies_FailsFast()
    {
        var options = new RunReportingOptions();
        IRunReportPublisher[] publishers = [];
        var logger = NullLogger<RunReporter>.Instance;

        Assert.Throws<ArgumentNullException>(() => new RunReporter(null!, publishers, logger));
        Assert.Throws<ArgumentNullException>(() => new RunReporter(options, null!, logger));
        Assert.Throws<ArgumentNullException>(() => new RunReporter(options, publishers, null!));
    }

    [Fact]
    public async Task CompleteAsync_PublisherInternalTimeout_IsContainedAndOthersStillRun()
    {
        var healthy = new CapturingPublisher();
        var reporter = new RunReporter(
            new RunReportingOptions(),
            [new TimingOutPublisher(), healthy],
            NullLogger<RunReporter>.Instance);

        reporter.AddIssue("Row skipped.");
        await reporter.CompleteAsync();

        Assert.Single(healthy.Sent);
    }

    [Fact]
    public async Task CompleteAsync_WhenOnePublisherFails_DoesNotThrowAndStillReachesOthers()
    {
        var failing = new ThrowingPublisher();
        var healthy = new CapturingPublisher();
        var reporter = new RunReporter(
            new RunReportingOptions(),
            [failing, healthy],
            NullLogger<RunReporter>.Instance);

        reporter.AddIssue("Row skipped.");
        await reporter.CompleteAsync();

        Assert.Single(healthy.Sent);
    }

    [Fact]
    public async Task EmailPublisher_AttemptsCompactVariantWhenFullVariantFails()
    {
        var formatter = new FailingVariantFormatter();
        var publisher = new EmailRunReportPublisher(
            new RunReportingOptions
            {
                From = "reporter@test.local",
                To = "full@test.local",
                CompactTo = "compact@test.local"
            },
            formatter);
        var report = new RunReport(
            new Dictionary<string, string?>(),
            RunOutcome.Succeeded,
            DateTimeOffset.UtcNow,
            [],
            []);

        var exception = await Assert.ThrowsAsync<AggregateException>(() =>
            publisher.PublishAsync(report, CancellationToken.None));

        Assert.True(formatter.FullCalled);
        Assert.True(formatter.CompactCalled);
        Assert.Equal(2, exception.InnerExceptions.Count);
    }

    [Fact]
    public async Task CompleteAsync_EmptyReport_SkipsSendWhenConfiguredOff()
    {
        var sender = new CapturingPublisher();
        var reporter = new RunReporter(
            new RunReportingOptions { SendEmptyReports = false },
            [sender],
            NullLogger<RunReporter>.Instance);

        await reporter.CompleteAsync();

        Assert.Empty(sender.Sent);
    }

    [Fact]
    public async Task CompleteAsync_WithOnlyTables_SendsWhenEmptyReportSendIsOff()
    {
        var sender = new CapturingPublisher();
        var reporter = new RunReporter(
            new RunReportingOptions { SendEmptyReports = false },
            [sender],
            NullLogger<RunReporter>.Instance);

        reporter.AddTable(
            "Persisted",
            [new IdRow("INT-1")],
            [TableColumn<IdRow>.Left("ID", row => row.Id)]);
        await reporter.CompleteAsync();

        Assert.Single(sender.Sent);
    }

    [Fact]
    public async Task AddTable_UsesExplicitTypedColumnSelectors()
    {
        var reporter = CreateReporter(out var sender);

        reporter.AddTable(
            "Fetched Rates",
            [
                new RateRow("GBP", 1.25m),
                new RateRow("EUR", 1.1m)
            ],
            [
                TableColumn<RateRow>.Left("CURRENCY", row => row.Ccy),
                TableColumn<RateRow>.Number("RATE", row => row.Rate, "N2")
            ]);
        await reporter.CompleteAsync();

        var table = Assert.Single(Assert.Single(sender.Sent).Tables);
        Assert.Equal("Fetched Rates", table.Title);
        Assert.Equal(["CURRENCY", "RATE"], table.Headers);
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
                new RunIssue("Step<1>", "Internal<Id>", "row<1>", IssueSeverity.Error, "<script>alert(1)</script>"),
                new RunIssue("Step<2>", "InternalId", "row<2>", IssueSeverity.Warning, "Review this row.")
            ],
            [new ResultTable(
                "Persisted <Rows>",
                ["ID"],
                [ColumnAlignment.Left],
                [["INT-1"]])]);

        var email = await formatter.FormatAsync(report, CancellationToken.None);

        Assert.Contains("[TestApp] Run failed", email.Subject);
        Assert.DoesNotContain("<script>", email.HtmlBody);
        Assert.Contains("&lt;script&gt;", email.HtmlBody);
        Assert.Contains("Step&lt;1&gt;", email.HtmlBody);
        Assert.Contains("Internal&lt;Id&gt;", email.HtmlBody);
        Assert.Contains("row&lt;1&gt;", email.HtmlBody);
        Assert.Contains(">IDENTIFIER</th>", email.HtmlBody);
        Assert.Contains(">VALUE</th>", email.HtmlBody);
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
        reporter.AddIssue("Nested", "Id", "42", "Subject message.");
    }

    private static RunReporter CreateReporter(out CapturingPublisher sender)
    {
        sender = new CapturingPublisher();
        return new RunReporter(
            new RunReportingOptions(),
            [sender],
            NullLogger<RunReporter>.Instance);
    }

    private sealed record RateRow(string Ccy, decimal Rate);

    private sealed record IdRow(string Id);

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

    private sealed class FailingVariantFormatter : IRunReportFormatter
    {
        public bool FullCalled { get; private set; }

        public bool CompactCalled { get; private set; }

        public Task<RunReportEmail> FormatAsync(RunReport report, CancellationToken cancellationToken)
        {
            FullCalled = true;
            throw new InvalidOperationException("Full variant failed.");
        }

        public Task<RunReportEmail> FormatCompactAsync(RunReport report, CancellationToken cancellationToken)
        {
            CompactCalled = true;
            throw new InvalidOperationException("Compact variant failed.");
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

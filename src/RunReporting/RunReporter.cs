// Thread-safe reporter, one instance per process. BeginRun sets the ambient run for the
// current async flow the same way ILogger scopes do; without one, a default run collects.
// Publishing hands the report to every registered publisher; a publisher failure is logged,
// never thrown — reporting must not fail the run it reports on.
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace RunReporting;

public sealed class RunReporter : IRunReporter
{
    private readonly AsyncLocal<RunScope?> _ambientRun = new();
    private readonly RunScope _defaultRun;
    private readonly RunReportingOptions _options;
    private readonly IReadOnlyList<IRunReportPublisher> _publishers;
    private readonly ILogger<RunReporter> _logger;

    public RunReporter(
        RunReportingOptions options,
        IEnumerable<IRunReportPublisher> publishers,
        ILogger<RunReporter>? logger = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _publishers = publishers?.ToList() ?? throw new ArgumentNullException(nameof(publishers));
        _logger = logger ?? NullLogger<RunReporter>.Instance;
        _defaultRun = new RunScope(this, RunIssue.EmptyData, previousRun: null);
    }

    public IDisposable BeginRun(IReadOnlyDictionary<string, string?>? attributes = null)
    {
        var scope = new RunScope(this, attributes ?? RunIssue.EmptyData, _ambientRun.Value);
        _ambientRun.Value = scope;
        return scope;
    }

    public IDisposable BeginRun(params (string Name, string? Value)[] attributes)
    {
        var dictionary = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, value) in attributes)
        {
            dictionary[name] = value;
        }

        return BeginRun(dictionary);
    }

    public void AddAttribute(string name, string? value)
    {
        if (name == null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        CurrentRun.SetAttribute(name, value);
    }

    public void AddIssue(string message, IssueSeverity severity = IssueSeverity.Warning)
    {
        Report(RunIssue.Create(null, severity, message));
    }

    public void AddIssue(object? subject, string message, string? stepName = null, IssueSeverity severity = IssueSeverity.Warning)
    {
        Report(RunIssue.Create(stepName, severity, message, IssueData.From(subject)));
    }

    public void AddTable(string title, IReadOnlyList<string> fields, IEnumerable<object?> rows)
    {
        CurrentRun.Add(ResultTable.From(title, fields, rows));
    }

    public RunReport Take()
    {
        return CurrentRun.Take();
    }

    public async Task<RunReport> PublishAsync(RunOutcome? outcome = null, CancellationToken cancellationToken = default)
    {
        var report = Take();
        if (outcome.HasValue)
        {
            report = report with { Outcome = outcome.Value };
        }

        await PublishAsync(report, cancellationToken).ConfigureAwait(false);
        return report;
    }

    public async Task PublishAsync(RunReport report, CancellationToken cancellationToken = default)
    {
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }

        if (!report.HasIssues && report.Tables.Count == 0 && !_options.SendWhenNoIssues)
        {
            return;
        }

        foreach (var publisher in _publishers)
        {
            try
            {
                await publisher.PublishAsync(report, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Run report publisher {Publisher} failed; the report was not delivered through it.",
                    publisher.GetType().Name);
            }
        }
    }

    private void Report(RunIssue issue)
    {
        CurrentRun.Add(issue);
        _logger.Log(
            issue.Severity == IssueSeverity.Error ? LogLevel.Error : LogLevel.Warning,
            "{StepName} {Severity}: {Message}. Data: {@Data}",
            string.IsNullOrEmpty(issue.StepName) ? "General" : issue.StepName,
            issue.Severity,
            issue.Message,
            issue.Data);
    }

    private RunScope CurrentRun => _ambientRun.Value ?? _defaultRun;

    private sealed class RunScope(
        RunReporter reporter,
        IReadOnlyDictionary<string, string?> attributes,
        RunScope? previousRun) : IDisposable
    {
        private readonly object _gate = new();
        private readonly Dictionary<string, string?> _attributes = new(attributes, StringComparer.OrdinalIgnoreCase);
        private readonly List<RunIssue> _issues = [];
        private readonly List<ResultTable> _tables = [];
        private bool _disposed;

        public void SetAttribute(string name, string? value)
        {
            lock (_gate)
            {
                _attributes[name] = value;
            }
        }

        public void Add(RunIssue issue)
        {
            lock (_gate)
            {
                _issues.Add(issue);
            }
        }

        public void Add(ResultTable table)
        {
            lock (_gate)
            {
                _tables.Add(table);
            }
        }

        public RunReport Take()
        {
            Dictionary<string, string?> attributeSnapshot;
            List<RunIssue> issues;
            List<ResultTable> tables;
            lock (_gate)
            {
                // Issues and tables drain; attributes describe the run and survive the Take.
                attributeSnapshot = new Dictionary<string, string?>(_attributes, StringComparer.OrdinalIgnoreCase);
                issues = [.. _issues];
                tables = [.. _tables];
                _issues.Clear();
                _tables.Clear();
            }

            return new RunReport(
                attributeSnapshot,
                RunReport.DeriveOutcome(issues),
                DateTimeOffset.UtcNow,
                issues,
                tables);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (ReferenceEquals(reporter._ambientRun.Value, this))
            {
                reporter._ambientRun.Value = previousRun;
            }
        }
    }
}

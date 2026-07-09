// Thread-safe reporter, one instance per process. BeginRun sets the ambient run for the
// current async flow the same way ILogger scopes do; without one, a default run collects.
// Like a logger, no member throws at runtime: failures are logged and degraded, never
// propagated to the caller. The one exception is the caller's own cancellation token,
// which PublishAsync honors.
using Microsoft.Extensions.Logging;

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
        ILogger<RunReporter> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(publishers);
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
        _options = options;
        _publishers = publishers.ToList();
        _defaultRun = new RunScope(this, new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase), previousRun: null);
    }

    public IDisposable BeginRun(IReadOnlyDictionary<string, string?>? attributes = null)
    {
        return BeginRun(CopyAttributes(attributes));
    }

    public IDisposable BeginRun(params (string Name, string? Value)[] attributes)
    {
        return BeginRun(CopyAttributes(
            (attributes ?? []).Select(attribute => new KeyValuePair<string, string?>(attribute.Name, attribute.Value))));
    }

    private IDisposable BeginRun(Dictionary<string, string?> attributes)
    {
        var scope = new RunScope(this, attributes, _ambientRun.Value);
        _ambientRun.Value = scope;
        return scope;
    }

    // Copies entry by entry so one bad attribute (empty name, case-colliding key, a source that
    // throws mid-enumeration) costs only itself, never the whole set. Last value wins, like config.
    private Dictionary<string, string?> CopyAttributes(IEnumerable<KeyValuePair<string, string?>>? attributes)
    {
        var copy = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var attribute in attributes ?? [])
            {
                if (string.IsNullOrEmpty(attribute.Key))
                {
                    _logger.LogError("Run reporting ignored a run attribute without a name.");
                    continue;
                }

                copy[attribute.Key] = attribute.Value;
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Run reporting failed to read all run attributes; the run starts with the ones read so far.");
        }

        return copy;
    }

    public void AddAttribute(string name, string? value)
    {
        if (string.IsNullOrEmpty(name))
        {
            _logger.LogError("Run reporting ignored a run attribute without a name.");
            return;
        }

        CurrentRun.SetAttribute(name, value);
    }

    public void AddIssue(string message, IssueSeverity severity = IssueSeverity.Warning)
    {
        AddIssue(string.Empty, string.Empty, string.Empty, message, severity);
    }

    public void AddIssue(
        string stepName,
        string identifierName,
        string identifierValue,
        string message,
        IssueSeverity severity = IssueSeverity.Warning)
    {
        try
        {
            var issue = new RunIssue(
                stepName ?? string.Empty,
                identifierName ?? string.Empty,
                identifierValue ?? string.Empty,
                severity == IssueSeverity.Error ? IssueSeverity.Error : IssueSeverity.Warning,
                message ?? string.Empty);

            CurrentRun.Add(issue);
            _logger.Log(
                issue.Severity == IssueSeverity.Error ? LogLevel.Error : LogLevel.Warning,
                "{StepName} {Severity}: {Message}. {IdentifierName}: {IdentifierValue}",
                string.IsNullOrWhiteSpace(issue.StepName) ? "General" : issue.StepName,
                issue.Severity,
                issue.Message,
                issue.IdentifierName,
                issue.IdentifierValue);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Run reporting failed to record an issue; it will be missing from the report.");
        }
    }

    public void AddTable<TRow>(
        string title,
        IEnumerable<TRow> rows,
        IReadOnlyList<TableColumn<TRow>> columns)
    {
        try
        {
            CurrentRun.Add(ResultTable.From(title, rows, columns));
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Run reporting failed to record table {Title}; it will be missing from the report.",
                title);
        }
    }

    public RunReport Take()
    {
        return CurrentRun.Take();
    }

    public async Task<RunReport> PublishAsync(RunOutcome? outcome = null, CancellationToken cancellationToken = default)
    {
        // A request already cancelled by the caller must not drain the current run.
        cancellationToken.ThrowIfCancellationRequested();

        var report = Take();
        if (outcome.HasValue)
        {
            report = report with { Outcome = outcome.Value };
        }

        await PublishReportAsync(report, cancellationToken).ConfigureAwait(false);
        return report;
    }

    private async Task PublishReportAsync(RunReport report, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!report.HasIssues && report.Tables.Count == 0 && !_options.SendWhenNoIssues)
        {
            return;
        }

        foreach (var publisher in _publishers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await publisher.PublishAsync(report, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The caller asked to cancel; honoring it is the only exception that may leave this method.
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

        // Do not report successful completion if a publisher ignored a cancellation
        // requested while it was running.
        cancellationToken.ThrowIfCancellationRequested();
    }

    private RunScope CurrentRun => _ambientRun.Value ?? _defaultRun;

    private sealed class RunScope(
        RunReporter reporter,
        Dictionary<string, string?> attributes,
        RunScope? previousRun) : IDisposable
    {
        private readonly object _gate = new();
        private readonly RunScope? _previousRun = previousRun;
        private readonly Dictionary<string, string?> _attributes = attributes;
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
                reporter._ambientRun.Value = PreviousActiveRun();
            }
        }

        private RunScope? PreviousActiveRun()
        {
            var candidate = _previousRun;
            while (candidate?._disposed == true)
            {
                candidate = candidate._previousRun;
            }

            return candidate;
        }
    }
}

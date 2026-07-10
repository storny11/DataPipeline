// Thread-safe state for one DI-scoped run. Runtime reporting failures are logged and
// contained so they never replace the business outcome.
using Microsoft.Extensions.Logging;

namespace RunReporting;

public sealed class RunReporter : IRunReporter
{
    private readonly object _gate = new();
    private readonly Dictionary<string, string?> _attributes = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<RunIssue> _issues = [];
    private readonly List<ResultTable> _tables = [];
    private readonly RunReportingOptions _options;
    private readonly IReadOnlyList<IRunReportPublisher> _publishers;
    private readonly ILogger<RunReporter> _logger;
    private Task<RunReport>? _completion;

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
    }

    public void AddAttribute(string name, string? value)
    {
        if (string.IsNullOrEmpty(name))
        {
            _logger.LogError("Run reporting ignored a run attribute without a name.");
            return;
        }

        lock (_gate)
        {
            if (_completion != null)
            {
                _logger.LogWarning("Run reporting ignored attribute {AttributeName} after the run completed.", name);
                return;
            }

            _attributes[name] = value;
        }
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

            lock (_gate)
            {
                if (_completion != null)
                {
                    _logger.LogWarning("Run reporting ignored an issue after the run completed.");
                    return;
                }

                _issues.Add(issue);
            }

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
            var table = ResultTable.From(title, rows, columns);
            lock (_gate)
            {
                if (_completion != null)
                {
                    _logger.LogWarning("Run reporting ignored table {Title} after the run completed.", title);
                    return;
                }

                _tables.Add(table);
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Run reporting failed to record table {Title}; it will be missing from the report.",
                title);
        }
    }

    public Task<RunReport> CompleteAsync(RunOutcome? outcome = null)
    {
        TaskCompletionSource<RunReport> completionSource;
        RunReport report;
        lock (_gate)
        {
            if (_completion != null)
            {
                return _completion;
            }

            completionSource = new TaskCompletionSource<RunReport>(TaskCreationOptions.RunContinuationsAsynchronously);
            _completion = completionSource.Task;

            var issues = _issues.ToArray();
            report = new RunReport(
                new Dictionary<string, string?>(_attributes, StringComparer.OrdinalIgnoreCase),
                outcome ?? RunReport.DeriveOutcome(issues),
                DateTimeOffset.UtcNow,
                issues,
                _tables.ToArray());
        }

        _ = CompleteCoreAsync(report, completionSource);
        return completionSource.Task;
    }

    private async Task CompleteCoreAsync(
        RunReport report,
        TaskCompletionSource<RunReport> completionSource)
    {
        try
        {
            await PublishReportAsync(report).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Run reporting failed while completing the report.");
        }
        finally
        {
            completionSource.TrySetResult(report);
        }
    }

    private async Task PublishReportAsync(RunReport report)
    {
        if (report.Issues.Count == 0 && report.Tables.Count == 0 && !_options.SendEmptyReports)
        {
            return;
        }

        foreach (var publisher in _publishers)
        {
            try
            {
                await publisher.PublishAsync(report, CancellationToken.None).ConfigureAwait(false);
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
}

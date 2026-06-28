// Stores run progress snapshots in memory for local status endpoints.
using DataRetriever.Execution;

namespace DataRetriever.Monitoring;

public sealed class InMemoryProcessingTracker : IProcessingTracker
{
    // Deliberately simple for the one-run-at-a-time prototype; use locks or a concurrent store if parallel runs are enabled.
    private readonly Dictionary<Guid, RunState> _runs = new();
    private Guid? _latestRunId;
    private DateTimeOffset? _lastSuccessfulRunCompletedAt;

    public IRunInstrumentation ForRun(Guid runId)
    {
        if (!_runs.TryGetValue(runId, out var state))
        {
            state = new RunState(runId);
            _runs[runId] = state;
        }

        _latestRunId = runId;
        return new RunInstrumentation(state, this);
    }

    public Task<ProcessingRunSnapshot?> GetSnapshotAsync(
        Guid runId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_runs.TryGetValue(runId, out var state)
            ? state.ToSnapshot(_lastSuccessfulRunCompletedAt)
            : null);
    }

    public Task<ProcessingRunSnapshot?> GetLatestSnapshotAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_latestRunId is null)
        {
            return Task.FromResult<ProcessingRunSnapshot?>(ProcessingRunSnapshot.NeverRun);
        }

        return GetSnapshotAsync(_latestRunId.Value, cancellationToken);
    }

    private void MarkSuccessful(DateTimeOffset completedAt)
    {
        _lastSuccessfulRunCompletedAt = completedAt;
    }

    private sealed class RunInstrumentation(
        RunState state,
        InMemoryProcessingTracker owner) : IRunInstrumentation
    {
        public void AppendInstrumentationInfo(string level, IInstrumentationInfo info)
        {
            var now = DateTimeOffset.UtcNow;
            state.Append(level, info.Values, now);

            if (!string.Equals(level, "run", StringComparison.OrdinalIgnoreCase) ||
                !info.Values.TryGetValue("Status", out var statusValue))
            {
                return;
            }

            if (statusValue is RunStatus status ||
                Enum.TryParse(statusValue?.ToString(), ignoreCase: true, out status))
            {
                state.SetStatus(status, now);
                if (status == RunStatus.Success)
                {
                    owner.MarkSuccessful(now);
                }
            }
        }
    }

    private sealed class RunState(Guid runId)
    {
        private readonly Dictionary<string, Dictionary<string, object?>> _levels = new(StringComparer.OrdinalIgnoreCase);
        private RunStatus _status = RunStatus.NeverRun;
        private DateTimeOffset? _startedAt;
        private DateTimeOffset? _completedAt;

        public void Append(string level, IReadOnlyDictionary<string, object?> values, DateTimeOffset now)
        {
            if (!_levels.TryGetValue(level, out var levelValues))
            {
                levelValues = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                _levels[level] = levelValues;
            }

            foreach (var value in values)
            {
                levelValues[value.Key] = value.Value;
            }

            _startedAt ??= now;
        }

        public void SetStatus(RunStatus status, DateTimeOffset now)
        {
            _status = status;
            if (status == RunStatus.Running)
            {
                _startedAt ??= now;
                _completedAt = null;
            }
            else
            {
                _completedAt = now;
            }
        }

        public ProcessingRunSnapshot ToSnapshot(DateTimeOffset? lastSuccessfulRunCompletedAt)
        {
            var values = _levels.ToDictionary(
                level => level.Key,
                level => (IReadOnlyDictionary<string, object?>)level.Value.ToDictionary(
                    value => value.Key,
                    value => value.Value,
                    StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);

            return new ProcessingRunSnapshot(
                runId,
                _status,
                _startedAt,
                _completedAt,
                lastSuccessfulRunCompletedAt,
                values);
        }
    }
}

using System.Collections.Concurrent;
using DataRetriever.Execution;

namespace DataRetriever.Monitoring;

public sealed class InMemoryProcessingTracker : IProcessingTracker
{
    private readonly ConcurrentDictionary<Guid, RunState> _runs = new();
    private readonly object _latestLock = new();
    private Guid? _latestRunId;
    private DateTimeOffset? _lastSuccessfulRunCompletedAt;

    public IRunInstrumentation ForRun(Guid runId)
    {
        var state = _runs.GetOrAdd(runId, id => new RunState(id));
        lock (_latestLock)
        {
            _latestRunId = runId;
        }

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
        Guid? latest;
        lock (_latestLock)
        {
            latest = _latestRunId;
        }

        if (latest is null)
        {
            return Task.FromResult<ProcessingRunSnapshot?>(ProcessingRunSnapshot.NeverRun);
        }

        return GetSnapshotAsync(latest.Value, cancellationToken);
    }

    private void MarkSuccessful(DateTimeOffset completedAt)
    {
        lock (_latestLock)
        {
            _lastSuccessfulRunCompletedAt = completedAt;
        }
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
        private readonly object _lock = new();
        private readonly Dictionary<string, Dictionary<string, object?>> _levels = new(StringComparer.OrdinalIgnoreCase);
        private RunStatus _status = RunStatus.NeverRun;
        private DateTimeOffset? _startedAt;
        private DateTimeOffset? _completedAt;

        public void Append(string level, IReadOnlyDictionary<string, object?> values, DateTimeOffset now)
        {
            lock (_lock)
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
        }

        public void SetStatus(RunStatus status, DateTimeOffset now)
        {
            lock (_lock)
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
        }

        public ProcessingRunSnapshot ToSnapshot(DateTimeOffset? lastSuccessfulRunCompletedAt)
        {
            lock (_lock)
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
}

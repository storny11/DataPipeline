// Provides a simple simulated run instrumentation implementation.
using DataRetriever.Monitoring;

namespace DataRetriever.Simulators.Monitoring;

public sealed class SimulatedRunInstrumentation : IRunInstrumentation
{
    private readonly Dictionary<string, Dictionary<string, object?>> _levels = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>> Levels =>
        _levels.ToDictionary(
            level => level.Key,
            level => (IReadOnlyDictionary<string, object?>)level.Value,
            StringComparer.OrdinalIgnoreCase);

    public void AppendInstrumentationInfo(string level, IInstrumentationInfo info)
    {
        if (!_levels.TryGetValue(level, out var values))
        {
            values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            _levels[level] = values;
        }

        foreach (var value in info.Values)
        {
            values[value.Key] = value.Value;
        }
    }
}

using DataRetriever.Monitoring;

namespace DataRetriever.Simulators.Monitoring;

public sealed class SimulatedInstrumentationInfo : IInstrumentationInfo
{
    private readonly Dictionary<string, object?> _values = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, object?> Values => _values;

    public void AddValue<T>(string name, T value)
    {
        _values[name] = value;
    }
}

// Defines the value collector shape used when appending progress instrumentation.
namespace DataRetriever.Monitoring;

public interface IInstrumentationInfo
{
    IReadOnlyDictionary<string, object?> Values { get; }

    void AddValue<T>(string name, T value);
}

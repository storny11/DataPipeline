// Defines how a run appends progress values to a named level.
namespace DataRetriever.Monitoring;

public interface IRunInstrumentation
{
    void AppendInstrumentationInfo(string level, IInstrumentationInfo info);
}

namespace DataRetriever.Monitoring;

public interface IRunInstrumentation
{
    void AppendInstrumentationInfo(string level, IInstrumentationInfo info);
}

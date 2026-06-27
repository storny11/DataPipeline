// Writes run and step progress values to the monitoring abstraction.
using DataRetriever.Execution;
using DataRetriever.Monitoring;

namespace DataRetriever.Application.Runs;

public sealed class RunInstrumentationWriter
{
    public void RecordRunStatus(IRunInstrumentation instrumentation, RunStatus status)
    {
        var info = new InstrumentationInfo();
        info.AddValue("Status", status);
        instrumentation.AppendInstrumentationInfo("run", info);
    }

    public void RecordStepResult(IRunInstrumentation instrumentation, IStepExecutionResult result)
    {
        var info = new InstrumentationInfo();
        info.AddValue("Status", result.Status.ToString());
        info.AddValue("Warnings", result.Issues.Count(issue => issue.Severity == StepIssueSeverity.Warning));
        info.AddValue("Errors", result.Issues.Count(issue => issue.Severity == StepIssueSeverity.Error));

        foreach (var counter in result.Counters)
        {
            info.AddValue(counter.Name, counter.Value);
        }

        instrumentation.AppendInstrumentationInfo(result.StepName, info);
    }

    private sealed class InstrumentationInfo : IInstrumentationInfo
    {
        private readonly Dictionary<string, object?> _values = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<string, object?> Values => _values;

        public void AddValue<T>(string name, T value)
        {
            _values[name] = value;
        }
    }
}

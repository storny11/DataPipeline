// Defines the common executable contract for each application step.
namespace DataRetriever.Execution;

public interface IStep<TInput, TOutput>
{
    string Name { get; }

    Task<StepExecutionResult<TOutput>> ExecuteAsync(
        TInput input,
        RunContext context,
        CancellationToken cancellationToken);
}

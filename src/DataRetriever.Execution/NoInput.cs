// Represents a step input placeholder when a step has no meaningful input.
namespace DataRetriever.Execution;

public readonly record struct NoInput
{
    public static readonly NoInput Value = new();
}

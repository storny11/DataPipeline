// Represents a step output placeholder when a step has no meaningful output.
namespace DataRetriever.Execution;

public readonly record struct NoOutput
{
    public static readonly NoOutput Value = new();
}

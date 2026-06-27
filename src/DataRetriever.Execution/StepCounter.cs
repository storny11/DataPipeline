// Represents a named numeric measurement produced by a step.
namespace DataRetriever.Execution;

public sealed record StepCounter(string Name, long Value);

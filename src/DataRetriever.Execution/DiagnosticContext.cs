// Carries row identifiers and other diagnostics for issues, logs, and reports.
namespace DataRetriever.Execution;

public sealed record DiagnosticContext(
    IReadOnlyDictionary<string, string?> Values)
{
    public static DiagnosticContext From(params (string Name, string? Value)[] values)
    {
        return new DiagnosticContext(values.ToDictionary(
            value => value.Name,
            value => value.Value,
            StringComparer.OrdinalIgnoreCase));
    }
}

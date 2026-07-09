// Builds display-only issue data from explicit named string values.

namespace RunReporting;

public static class IssueData
{
    public static IReadOnlyDictionary<string, string?> From(
        params (string Name, string? Value)[] values)
    {
        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values ?? [])
        {
            if (!string.IsNullOrWhiteSpace(value.Name))
            {
                data[value.Name] = value.Value;
            }
        }

        return data;
    }
}

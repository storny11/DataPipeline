// A published result set: field names plus formatted rows, built from plain or anonymous objects.
// Fields are the column headers shown verbatim; row values are matched to them ignoring
// case and spacing, so "INTERNAL ID" finds an InternalId property.
using System.Collections.Concurrent;
using System.Reflection;
using System.Text;

namespace RunReporting;

public sealed record ResultTable(
    string Title,
    IReadOnlyList<string> Fields,
    IReadOnlyList<ColumnAlignment> Alignments,
    IReadOnlyList<IReadOnlyList<string?>> Rows)
{
    private static readonly ConcurrentDictionary<Type, Dictionary<string, PropertyInfo>> PropertyCache = new();

    /// <summary>Builds a table from rows: plain or anonymous objects, one cell per field.</summary>
    public static ResultTable From(
        string? title,
        IReadOnlyList<string>? fields,
        IEnumerable<object?>? rows,
        IReadOnlyList<ColumnAlignment>? alignments = null)
    {
        var safeFields = (fields ?? []).Select(field => field ?? string.Empty).ToList();
        var safeAlignments = Enumerable.Range(0, safeFields.Count)
            .Select(index => alignments != null && index < alignments.Count ? alignments[index] : ColumnAlignment.Left)
            .ToList();
        var normalizedFields = safeFields.Select(NormalizeKey).ToList();

        return new ResultTable(
            title ?? string.Empty,
            safeFields,
            safeAlignments,
            (rows ?? []).Select(row => ToRow(row, normalizedFields)).ToList());
    }

    private static IReadOnlyList<string?> ToRow(object? row, IReadOnlyList<string> normalizedFields)
    {
        if (row == null)
        {
            return new string?[normalizedFields.Count];
        }

        var properties = PropertyCache.GetOrAdd(row.GetType(), BuildPropertyMap);

        return normalizedFields
            .Select(field => properties.TryGetValue(field, out var property)
                ? ValueFormatter.FormatProperty(property, row)
                : null)
            .ToList();
    }

    private static Dictionary<string, PropertyInfo> BuildPropertyMap(Type type)
    {
        var map = new Dictionary<string, PropertyInfo>();
        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (property.CanRead && property.GetIndexParameters().Length == 0)
            {
                map.TryAdd(NormalizeKey(property.Name), property);
            }
        }

        return map;
    }

    // "INTERNAL ID" -> "internalid", "InternalId" -> "internalid"
    private static string NormalizeKey(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString();
    }
}

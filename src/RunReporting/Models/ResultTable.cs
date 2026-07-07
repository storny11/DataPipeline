// A published result set: field names plus formatted rows, built from dictionaries or objects.
using System.Collections.Concurrent;
using System.Reflection;

namespace RunReporting;

public sealed record ResultTable(
    string Title,
    IReadOnlyList<string> Fields,
    IReadOnlyList<IReadOnlyList<string?>> Rows)
{
    private static readonly ConcurrentDictionary<Type, Dictionary<string, PropertyInfo>> PropertyCache = new();

    /// <summary>Builds a table from rows the way Dapper returns them: dictionary rows (dynamic) or plain objects, one cell per field.</summary>
    public static ResultTable From(string? title, IReadOnlyList<string>? fields, IEnumerable<object?>? rows)
    {
        var safeFields = (fields ?? []).Select(field => field ?? string.Empty).ToList();
        return new ResultTable(
            title ?? string.Empty,
            safeFields,
            (rows ?? []).Select(row => ToRow(row, safeFields)).ToList());
    }

    private static IReadOnlyList<string?> ToRow(object? row, IReadOnlyList<string> fields)
    {
        switch (row)
        {
            case null:
                return new string?[fields.Count];
            case IDictionary<string, object?> map:
                return fields
                    .Select(field => map.TryGetValue(field, out var value) ? ValueFormatter.Format(value) : null)
                    .ToList();
            default:
                return FromProperties(row, fields);
        }
    }

    private static IReadOnlyList<string?> FromProperties(object row, IReadOnlyList<string> fields)
    {
        var properties = PropertyCache.GetOrAdd(
            row.GetType(),
            type => type
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(property => property.CanRead && property.GetIndexParameters().Length == 0)
                .ToDictionary(property => property.Name, property => property, StringComparer.OrdinalIgnoreCase));

        return fields
            .Select(field => properties.TryGetValue(field, out var property)
                ? ReadProperty(property, row)
                : null)
            .ToList();
    }

    private static string? ReadProperty(PropertyInfo property, object row)
    {
        try
        {
            return ValueFormatter.Format(property.GetValue(row));
        }
        catch
        {
            // A throwing getter costs its cell, never the caller.
            return null;
        }
    }
}

// A published result set built from plain or anonymous row objects. Columns come from the
// first row's public properties in declaration order; headers are the property names
// humanized to spaced uppercase ("InternalId" -> "INTERNAL ID"). To choose or rename
// columns, project the rows into anonymous objects.
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
    private static readonly ConcurrentDictionary<Type, (PropertyInfo Property, string Key)[]> PropertyCache = new();

    public static ResultTable From(
        string? title,
        IEnumerable<object?>? rows,
        IReadOnlyList<ColumnAlignment>? alignments = null)
    {
        var rowList = (rows ?? []).ToList();
        var columns = rowList.FirstOrDefault(row => row != null) is { } first
            ? PropertiesOf(first.GetType())
            : [];

        var fields = columns.Select(column => ValueFormatter.ToHeader(column.Property.Name)).ToList();
        var normalizedFields = columns.Select(column => column.Key).ToList();
        var safeAlignments = Enumerable.Range(0, fields.Count)
            .Select(index => alignments != null && index < alignments.Count ? alignments[index] : ColumnAlignment.Left)
            .ToList();

        return new ResultTable(
            title ?? string.Empty,
            fields,
            safeAlignments,
            rowList.Select(row => ToRow(row, normalizedFields)).ToList());
    }

    private static IReadOnlyList<string?> ToRow(object? row, IReadOnlyList<string> normalizedFields)
    {
        if (row == null)
        {
            return new string?[normalizedFields.Count];
        }

        // Rows of a different type than the first still fill the cells they can,
        // matched by property name ignoring case.
        var properties = PropertiesOf(row.GetType());

        return normalizedFields
            .Select(field =>
            {
                var match = Array.Find(properties, column => column.Key == field);
                return match.Property != null ? ValueFormatter.FormatProperty(match.Property, row) : null;
            })
            .ToList();
    }

    private static (PropertyInfo Property, string Key)[] PropertiesOf(Type type)
    {
        return PropertyCache.GetOrAdd(type, static rowType => rowType
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.CanRead && property.GetIndexParameters().Length == 0)
            .Select(property => (property, NormalizeKey(property.Name)))
            .ToArray());
    }

    // "InternalId" -> "internalid"
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

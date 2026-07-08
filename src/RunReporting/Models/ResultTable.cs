// A published result set: column headers plus formatted rows built from plain or anonymous
// objects. Headers are shown verbatim; each header is matched to a row property ignoring
// case and spacing, so "INTERNAL ID" reads an InternalId property.
using System.Collections.Concurrent;
using System.Reflection;
using System.Text;

namespace RunReporting;

public sealed record ResultTable(
    string Title,
    IReadOnlyList<string> Headers,
    IReadOnlyList<ColumnAlignment> Alignments,
    IReadOnlyList<IReadOnlyList<string?>> Rows)
{
    private static readonly ConcurrentDictionary<Type, (PropertyInfo Property, string Key)[]> PropertyCache = new();

    public static ResultTable From(
        string? title,
        IReadOnlyList<string>? headers,
        IEnumerable<object?>? rows,
        IReadOnlyList<ColumnAlignment>? alignments = null)
    {
        var safeHeaders = (headers ?? []).Select(header => header ?? string.Empty).ToList();
        var normalizedHeaders = safeHeaders.Select(NormalizeKey).ToList();
        var safeAlignments = Enumerable.Range(0, safeHeaders.Count)
            .Select(index => alignments != null && index < alignments.Count ? alignments[index] : ColumnAlignment.Left)
            .ToList();

        return new ResultTable(
            title ?? string.Empty,
            safeHeaders,
            safeAlignments,
            (rows ?? []).Select(row => ToRow(row, normalizedHeaders)).ToList());
    }

    private static IReadOnlyList<string?> ToRow(object? row, IReadOnlyList<string> normalizedHeaders)
    {
        if (row == null)
        {
            return new string?[normalizedHeaders.Count];
        }

        var properties = PropertiesOf(row.GetType());

        return normalizedHeaders
            .Select(header =>
            {
                var match = Array.Find(properties, column => column.Key == header);
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

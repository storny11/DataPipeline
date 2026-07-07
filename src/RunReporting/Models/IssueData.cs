// Converts an arbitrary issue subject (scalar, ids, dictionary, or object) into diagnostic data.
using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;

namespace RunReporting;

public static class IssueData
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> PropertyCache = new();

    public static IReadOnlyDictionary<string, string?> From(object? subject)
    {
        switch (subject)
        {
            case null:
                return RunIssue.EmptyData;
            case IReadOnlyDictionary<string, string?> data:
                return data;
            case string id:
                return Single("id", id);
            case IDictionary<string, object?> objectData:
                return objectData.ToDictionary(pair => pair.Key, pair => Format(pair.Value));
            case IDictionary dictionary:
                return FromDictionary(dictionary);
            case IEnumerable enumerable:
                return Single("ids", string.Join(", ", enumerable.Cast<object?>().Select(Format)));
            default:
                return IsScalar(subject)
                    ? Single("id", Format(subject))
                    : FromProperties(subject);
        }
    }

    private static bool IsScalar(object subject)
    {
        var type = subject.GetType();
        return type.IsPrimitive || type.IsEnum || subject is IFormattable;
    }

    private static IReadOnlyDictionary<string, string?> FromDictionary(IDictionary dictionary)
    {
        var data = new Dictionary<string, string?>();
        foreach (DictionaryEntry entry in dictionary)
        {
            data[entry.Key?.ToString() ?? string.Empty] = Format(entry.Value);
        }

        return data;
    }

    private static IReadOnlyDictionary<string, string?> FromProperties(object subject)
    {
        var properties = PropertyCache.GetOrAdd(
            subject.GetType(),
            type => type
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(property => property.CanRead && property.GetIndexParameters().Length == 0)
                .ToArray());

        var data = new Dictionary<string, string?>();
        foreach (var property in properties)
        {
            data[property.Name] = ReadProperty(property, subject);
        }

        return data;
    }

    private static string? ReadProperty(PropertyInfo property, object subject)
    {
        try
        {
            return Format(property.GetValue(subject));
        }
        catch
        {
            // A throwing getter costs its value, never the caller.
            return null;
        }
    }

    private static IReadOnlyDictionary<string, string?> Single(string key, string? value)
    {
        return new Dictionary<string, string?> { [key] = value };
    }

    private static string? Format(object? value)
    {
        return ValueFormatter.Format(value);
    }
}

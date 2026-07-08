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
            case IEnumerable enumerable:
                return Single("ids", string.Join(", ", enumerable.Cast<object?>().Select(ValueFormatter.Format)));
            default:
                return IsScalar(subject)
                    ? Single("id", ValueFormatter.Format(subject))
                    : FromProperties(subject);
        }
    }

    private static bool IsScalar(object subject)
    {
        var type = subject.GetType();
        return type.IsPrimitive || type.IsEnum || subject is IFormattable;
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
            data[property.Name] = ValueFormatter.FormatProperty(property, subject);
        }

        return data;
    }

    private static IReadOnlyDictionary<string, string?> Single(string key, string? value)
    {
        return new Dictionary<string, string?> { [key] = value };
    }
}

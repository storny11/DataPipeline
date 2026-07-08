// Formats values culture-invariantly for issue data and result table cells.
using System.Globalization;
using System.Reflection;

namespace RunReporting;

internal static class ValueFormatter
{
    // "runId" -> "RUN ID", "ExternalId1" -> "EXTERNAL ID 1"
    public static string ToHeader(string name)
    {
        var builder = new System.Text.StringBuilder(name.Length + 4);
        for (var index = 0; index < name.Length; index++)
        {
            if (index > 0 && ShouldSeparate(name[index - 1], name[index]))
            {
                builder.Append(' ');
            }

            builder.Append(char.ToUpperInvariant(name[index]));
        }

        return builder.ToString();
    }

    private static bool ShouldSeparate(char previous, char current)
    {
        return (char.IsUpper(current) && char.IsLower(previous)) ||
            (char.IsDigit(current) && !char.IsDigit(previous)) ||
            (char.IsLetter(current) && char.IsDigit(previous));
    }

    public static string? FormatProperty(PropertyInfo property, object instance, string? format = null)
    {
        try
        {
            return Format(property.GetValue(instance), format);
        }
        catch
        {
            // A throwing getter costs its value, never the caller.
            return null;
        }
    }

    public static string? Format(object? value, string? format = null)
    {
        if (value == null)
        {
            return null;
        }

        if (value is IFormattable formattable)
        {
            if (format != null)
            {
                try
                {
                    return formattable.ToString(format, CultureInfo.InvariantCulture);
                }
                catch
                {
                    // A bad format string costs the formatting, never the value.
                }
            }

            try
            {
                return formattable.ToString(null, CultureInfo.InvariantCulture);
            }
            catch
            {
                return value.GetType().Name;
            }
        }

        try
        {
            return value.ToString();
        }
        catch
        {
            // A throwing ToString implementation costs the value, never the caller.
            return value.GetType().Name;
        }
    }
}

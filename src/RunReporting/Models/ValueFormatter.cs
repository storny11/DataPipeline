// Formats values culture-invariantly for issue data and result table cells.
using System.Globalization;

namespace RunReporting;

internal static class ValueFormatter
{
    public static string? Format(object? value)
    {
        try
        {
            return value switch
            {
                null => null,
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString()
            };
        }
        catch
        {
            // A throwing ToString/format implementation costs the value, never the caller.
            return value?.GetType().Name;
        }
    }
}

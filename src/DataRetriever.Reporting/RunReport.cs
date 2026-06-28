// Defines the structured report model shared by API responses and email formatting.
using System.Globalization;
using System.Reflection;
using System.Text;
using DataRetriever.Execution;

namespace DataRetriever.Reporting;

public sealed record RunReport(
    Guid RunId,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    RunStatus Status,
    RunRequestSummary Request,
    IReadOnlyList<RunReportMetric> Summary,
    IReadOnlyList<RunReportStep> Steps,
    IReadOnlyList<RunReportIssue> Issues,
    IReadOnlyList<RunReportTable> Tables)
{
    public int WarningCount => Issues.Count(issue => issue.Severity == StepIssueSeverity.Warning);

    public int ErrorCount => Issues.Count(issue => issue.Severity == StepIssueSeverity.Error);
}

public sealed record RunReportMetric(
    string Name,
    string Label,
    string Value);

public sealed record RunReportStep(
    string StepName,
    StepExecutionStatus Status,
    IReadOnlyList<StepCounter> Counters,
    int WarningCount,
    int ErrorCount);

public sealed record RunReportIssue(
    string StepName,
    StepIssueSeverity Severity,
    string Message,
    DiagnosticContext Context);

public sealed record RunRequestSummary(
    string? Currency,
    IReadOnlyList<string> InternalIds);

public sealed record RunReportTable(
    string Name,
    string Title,
    IReadOnlyList<RunReportColumn> Columns,
    IReadOnlyList<IReadOnlyDictionary<string, string?>> Rows)
{
    public static RunReportTable FromRows<T>(
        string name,
        string title,
        IEnumerable<T> rows)
    {
        var properties = typeof(T)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.CanRead && property.GetIndexParameters().Length == 0)
            .ToList();

        var columns = properties
            .Select(property => new RunReportColumn(
                ToKey(property.Name),
                ToHeader(property.Name),
                IsNumeric(property.PropertyType)
                    ? RunReportColumnAlignment.Right
                    : RunReportColumnAlignment.Left))
            .ToList();

        var tableRows = rows
            .Select(row => (IReadOnlyDictionary<string, string?>)properties.ToDictionary(
                property => ToKey(property.Name),
                property => FormatValue(property.GetValue(row)),
                StringComparer.OrdinalIgnoreCase))
            .ToList();

        return new RunReportTable(name, title, columns, tableRows);
    }

    private static string ToKey(string name)
    {
        return string.IsNullOrEmpty(name)
            ? name
            : char.ToLowerInvariant(name[0]) + name[1..];
    }

    private static string ToHeader(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        var builder = new StringBuilder(name.Length + 8);
        for (var index = 0; index < name.Length; index++)
        {
            var current = name[index];
            if (index > 0 && ShouldAddSpace(name[index - 1], current))
            {
                builder.Append(' ');
            }

            builder.Append(current);
        }

        return builder.ToString();
    }

    private static bool ShouldAddSpace(char previous, char current)
    {
        return (char.IsUpper(current) && (char.IsLower(previous) || char.IsDigit(previous))) ||
            (char.IsDigit(current) && !char.IsDigit(previous)) ||
            (!char.IsDigit(current) && char.IsDigit(previous));
    }

    private static bool IsNumeric(Type type)
    {
        var valueType = Nullable.GetUnderlyingType(type) ?? type;
        return valueType == typeof(byte) ||
            valueType == typeof(sbyte) ||
            valueType == typeof(short) ||
            valueType == typeof(ushort) ||
            valueType == typeof(int) ||
            valueType == typeof(uint) ||
            valueType == typeof(long) ||
            valueType == typeof(ulong) ||
            valueType == typeof(float) ||
            valueType == typeof(double) ||
            valueType == typeof(decimal);
    }

    private static string? FormatValue(object? value)
    {
        return value switch
        {
            null => null,
            decimal number => number.ToString("N4", CultureInfo.InvariantCulture),
            double number => number.ToString("N4", CultureInfo.InvariantCulture),
            float number => number.ToString("N4", CultureInfo.InvariantCulture),
            DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateTimeOffset dateTime => dateTime.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture),
            DateTime dateTime => dateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        };
    }
}

public sealed record RunReportColumn(
    string Key,
    string Header,
    RunReportColumnAlignment Alignment = RunReportColumnAlignment.Left);

public enum RunReportColumnAlignment
{
    Left,
    Right,
    Center
}

// Resolves one bootstrap-safe log file before full application configuration exists.
using System.Globalization;

namespace DataRetriever.Api.Hosting;

internal sealed record ApplicationLogPathResolution(
    string FilePath,
    string? ValidationError)
{
    public void ThrowIfInvalid()
    {
        if (ValidationError is not null)
        {
            throw new InvalidOperationException(ValidationError);
        }
    }
}

internal static class ApplicationLogPath
{
    private const string InstanceArgumentName = ApplicationLaunchArguments.InstanceArgumentName;

    public static ApplicationLogPathResolution Resolve(string? instanceName)
    {
        var logRoot = ResolveDefaultLogRoot();
        var applicationName = typeof(ApplicationLogPath).Assembly.GetName().Name ?? "application";
        var currentDate = DateTime.Today.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var processId = Environment.ProcessId.ToString(CultureInfo.InvariantCulture);
        var logFileName = $"{applicationName}_{currentDate}_{processId}.log";

        return Resolve(instanceName, logRoot, logFileName);
    }

    internal static ApplicationLogPathResolution Resolve(
        string? instanceName,
        string logRoot,
        string logFileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(logFileName);

        var fullLogRoot = Path.GetFullPath(logRoot);
        var validationError = GetPathSegmentValidationError(instanceName);
        var logDirectory = instanceName is not null && validationError is null
            ? Path.Combine(fullLogRoot, instanceName)
            : fullLogRoot;

        return new ApplicationLogPathResolution(
            Path.GetFullPath(Path.Combine(logDirectory, logFileName)),
            validationError);
    }

    private static string ResolveDefaultLogRoot()
    {
        if (!OperatingSystem.IsWindows())
        {
            return Path.Combine(AppContext.BaseDirectory, "logs");
        }

        return Directory.Exists(@"D:\")
            ? @"D:\Logs"
            : @"C:\Logs";
    }

    private static string? GetPathSegmentValidationError(string? value)
    {
        if (value is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return $"The '--{InstanceArgumentName}' command-line argument cannot be blank.";
        }

        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            return $"The '--{InstanceArgumentName}' command-line argument cannot have surrounding whitespace.";
        }

        if (value is "." or ".." ||
            Path.IsPathRooted(value) ||
            value.Contains('/') ||
            value.Contains('\\') ||
            value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return $"The '--{InstanceArgumentName}' command-line argument must be one safe directory name.";
        }

        return null;
    }
}

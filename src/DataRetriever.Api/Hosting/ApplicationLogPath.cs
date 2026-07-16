// Resolves one bootstrap-safe log path from a generic application-instance argument.
namespace DataRetriever.Api.Hosting;

internal static class ApplicationLogPath
{
    private const string InstanceArgumentName = "instance";

    public static string ResolveBootstrapFilePath(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var logRoot = Path.Combine(AppContext.BaseDirectory, "logs");
        var applicationName = typeof(ApplicationLogPath).Assembly.GetName().Name ?? "application";
        var logFileName = $"{applicationName}-.log";

        return ResolveBootstrapFilePath(args, logRoot, logFileName);
    }

    internal static string ResolveBootstrapFilePath(
        IReadOnlyList<string> args,
        string logRoot,
        string logFileName)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(logRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(logFileName);

        var instanceName = ReadInstanceName(args);
        var fullLogRoot = Path.GetFullPath(logRoot);
        var logDirectory = instanceName is null || GetPathSegmentValidationError(instanceName) is not null
            ? fullLogRoot
            : Path.Combine(fullLogRoot, instanceName);

        return Path.GetFullPath(Path.Combine(logDirectory, logFileName));
    }

    public static void EnsureLaunchIsValid(string[] args, string environmentName)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentName);

        var instanceName = ReadInstanceName(args);
        var validationError = GetPathSegmentValidationError(instanceName);
        if (validationError is null &&
            instanceName is null &&
            !string.Equals(environmentName, Environments.Development, StringComparison.OrdinalIgnoreCase))
        {
            validationError =
                $"The '--{InstanceArgumentName}' command-line argument is required outside Development.";
        }

        if (validationError is not null)
        {
            throw new InvalidOperationException(validationError);
        }
    }

    private static string? ReadInstanceName(IReadOnlyList<string> args)
    {
        return new ConfigurationBuilder()
            .AddCommandLine(args.ToArray())
            .Build()[InstanceArgumentName];
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

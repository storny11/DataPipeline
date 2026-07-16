// Resolves one bootstrap-safe log path from a generic application-instance argument.
namespace DataRetriever.Api.Hosting;

internal sealed record ApplicationLogPathResolution(
    string LogFilePath,
    string? ValidationError)
{
    public void EnsureLaunchIsValid()
    {
        if (!string.IsNullOrWhiteSpace(ValidationError))
        {
            throw new InvalidOperationException(ValidationError);
        }
    }
}

internal static class ApplicationLogPath
{
    private const string InstanceArgumentName = "instance";

    public static string ResolveBootstrapFilePath(string[] args)
    {
        return Resolve(args, Environments.Development).LogFilePath;
    }

    public static ApplicationLogPathResolution Resolve(string[] args, string environmentName)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentName);

        var logRoot = Path.Combine(AppContext.BaseDirectory, "logs");
        var applicationName = typeof(ApplicationLogPath).Assembly.GetName().Name ?? "application";
        var logFileName = $"{applicationName}-.log";

        return Resolve(args, environmentName, logRoot, logFileName);
    }

    internal static ApplicationLogPathResolution Resolve(
        string[] args,
        string environmentName,
        string logRoot,
        string logFileName)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentName);
        ArgumentException.ThrowIfNullOrWhiteSpace(logRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(logFileName);

        var commandLine = new ConfigurationBuilder()
            .AddCommandLine(args)
            .Build();
        var instanceName = commandLine[InstanceArgumentName];
        string? validationError = null;

        if (instanceName is not null)
        {
            validationError = GetPathSegmentValidationError(instanceName);
        }
        else if (!string.Equals(environmentName, Environments.Development, StringComparison.OrdinalIgnoreCase))
        {
            validationError =
                $"The '--{InstanceArgumentName}' command-line argument is required outside Development.";
        }

        var fullLogRoot = Path.GetFullPath(logRoot);
        var logDirectory = validationError is not null || instanceName is null
            ? fullLogRoot
            : Path.Combine(fullLogRoot, instanceName);

        return new ApplicationLogPathResolution(
            Path.GetFullPath(Path.Combine(logDirectory, logFileName)),
            validationError);
    }

    private static string? GetPathSegmentValidationError(string? value)
    {
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

// Resolves one bootstrap-safe log path from primitive process inputs only.
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
    private const string NamespaceArgumentName = "namespace";
    private const string EnvironmentArgumentName = "environment";

    public static ApplicationLogPathResolution Resolve(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var logRoot = Path.Combine(AppContext.BaseDirectory, "logs");
        var applicationName = typeof(ApplicationLogPath).Assembly.GetName().Name ?? "application";
        var logFileName = $"{applicationName}-.log";

        return Resolve(args, ResolveEnvironmentName(args), logRoot, logFileName);
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

        var namespaceArgument = FindArgument(args, NamespaceArgumentName);
        string? namespaceName = null;
        string? validationError = null;

        if (namespaceArgument.IsPresent)
        {
            validationError = GetPathSegmentValidationError(namespaceArgument.Value);
            if (validationError == null)
            {
                namespaceName = namespaceArgument.Value;
            }
        }
        else if (!string.Equals(environmentName, Environments.Development, StringComparison.OrdinalIgnoreCase))
        {
            validationError =
                $"The '--{NamespaceArgumentName}' command-line argument is required outside Development.";
        }

        var fullLogRoot = Path.GetFullPath(logRoot);
        var logDirectory = namespaceName == null
            ? fullLogRoot
            : Path.Combine(fullLogRoot, namespaceName);

        return new ApplicationLogPathResolution(
            Path.GetFullPath(Path.Combine(logDirectory, logFileName)),
            validationError);
    }

    private static string ResolveEnvironmentName(string[] args)
    {
        var environmentArgument = FindArgument(args, EnvironmentArgumentName);
        if (environmentArgument.IsPresent && !string.IsNullOrWhiteSpace(environmentArgument.Value))
        {
            return environmentArgument.Value;
        }

        return Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environments.Production;
    }

    private static string? GetPathSegmentValidationError(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return $"The '--{NamespaceArgumentName}' command-line argument cannot be blank.";
        }

        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            return $"The '--{NamespaceArgumentName}' command-line argument cannot have surrounding whitespace.";
        }

        if (value is "." or ".." ||
            Path.IsPathRooted(value) ||
            value.Contains(Path.DirectorySeparatorChar) ||
            value.Contains(Path.AltDirectorySeparatorChar) ||
            value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return $"The '--{NamespaceArgumentName}' command-line argument must be one safe directory name.";
        }

        return null;
    }

    private static CommandLineArgument FindArgument(IReadOnlyList<string> args, string key)
    {
        var result = new CommandLineArgument(IsPresent: false, Value: null);

        for (var index = 0; index < args.Count; index++)
        {
            var argument = args[index];
            if (string.IsNullOrWhiteSpace(argument))
            {
                continue;
            }

            var normalizedArgument = argument.TrimStart('-', '/');
            var separatorIndex = normalizedArgument.IndexOf('=');
            if (separatorIndex >= 0)
            {
                var candidateKey = normalizedArgument[..separatorIndex];
                if (candidateKey.Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    result = new CommandLineArgument(
                        IsPresent: true,
                        Value: normalizedArgument[(separatorIndex + 1)..]);
                }

                continue;
            }

            if (!normalizedArgument.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = index + 1 < args.Count && !LooksLikeOption(args[index + 1])
                ? args[++index]
                : string.Empty;

            result = new CommandLineArgument(IsPresent: true, Value: value);
        }

        return result;
    }

    private static bool LooksLikeOption(string argument)
    {
        return argument.StartsWith('-') || argument.StartsWith('/');
    }

    private readonly record struct CommandLineArgument(bool IsPresent, string? Value);
}

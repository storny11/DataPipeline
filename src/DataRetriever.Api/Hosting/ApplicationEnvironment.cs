// Resolves the logical host environment before WebApplicationBuilder fixes its environment name.
namespace DataRetriever.Api.Hosting;

internal static class ApplicationEnvironment
{
    public const string ArgumentName = ApplicationLaunchArguments.EnvironmentArgumentName;

    public static string ReadRequired(ApplicationLaunchArguments launchArguments)
    {
        ArgumentNullException.ThrowIfNull(launchArguments);

        return ReadRequired(launchArguments.Environment);
    }

    internal static string ReadRequired(string? commandLineEnvironment)
    {
        if (string.IsNullOrWhiteSpace(commandLineEnvironment))
        {
            throw new InvalidOperationException(
                $"The '--{ArgumentName}' command-line argument is required.");
        }

        if (!string.Equals(
                commandLineEnvironment,
                commandLineEnvironment.Trim(),
                StringComparison.Ordinal) ||
            commandLineEnvironment is "." or ".." ||
            commandLineEnvironment.Any(character =>
                !char.IsLetterOrDigit(character) && character is not '.' and not '-' and not '_'))
        {
            throw new InvalidOperationException(
                $"The '--{ArgumentName}' command-line argument must use only letters, numbers, '.', '-' or '_'.");
        }

        return commandLineEnvironment;
    }
}

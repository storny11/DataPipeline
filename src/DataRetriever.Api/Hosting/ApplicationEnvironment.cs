// Resolves the logical host environment before WebApplicationBuilder fixes its environment name.
namespace DataRetriever.Api.Hosting;

internal static class ApplicationEnvironment
{
    public const string ArgumentName = "env";

    public static string ReadRequired(string[] args)
    {
        return ReadRequired(
            args,
            Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT"),
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"));
    }

    internal static string ReadRequired(
        string[] args,
        string? dotnetEnvironment,
        string? aspNetCoreEnvironment)
    {
        ArgumentNullException.ThrowIfNull(args);

        var commandLine = new ConfigurationBuilder()
            .AddCommandLine(args)
            .Build();
        var environmentName = commandLine[ArgumentName]
            ?? dotnetEnvironment
            ?? aspNetCoreEnvironment;

        if (string.IsNullOrWhiteSpace(environmentName))
        {
            throw new InvalidOperationException(
                $"The '--{ArgumentName}' command-line argument is required when no .NET environment variable is set.");
        }

        if (!string.Equals(environmentName, environmentName.Trim(), StringComparison.Ordinal) ||
            environmentName is "." or ".." ||
            environmentName.Any(character =>
                !char.IsLetterOrDigit(character) && character is not '.' and not '-' and not '_'))
        {
            throw new InvalidOperationException(
                $"The '--{ArgumentName}' command-line argument must use only letters, numbers, '.', '-' or '_'.");
        }

        return environmentName;
    }
}

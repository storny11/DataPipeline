// Parses only the primitive selectors required before application configuration exists.
namespace DataRetriever.Api.Hosting;

internal sealed record ApplicationLaunchArguments(
    string? Environment,
    string? Instance,
    string? ExternalConfigurationProfile)
{
    public const string EnvironmentArgumentName = "environment";
    public const string InstanceArgumentName = "instance";
    public const string ExternalConfigurationProfileArgumentName = "externalProfile";

    public static ApplicationLaunchArguments Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var commandLine = new ConfigurationBuilder()
            .AddCommandLine(args)
            .Build();

        return new ApplicationLaunchArguments(
            commandLine[EnvironmentArgumentName],
            commandLine[InstanceArgumentName],
            commandLine[ExternalConfigurationProfileArgumentName]);
    }
}

// Defines whether the host uses simulator adapters or real infrastructure adapters at composition time.
namespace DataRetriever.Api.Composition;

public enum AdapterMode
{
    Simulator,
    Real
}

public static class AdapterModeConfiguration
{
    public const string ConfigurationKey = "AdapterMode";

    public static AdapterMode ReadRequired(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var configured = configuration[ConfigurationKey];
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException(
                $"Required configuration value '{ConfigurationKey}' is missing.");
        }

        if (!Enum.TryParse<AdapterMode>(configured, ignoreCase: true, out var mode) ||
            !Enum.IsDefined(mode))
        {
            throw new InvalidOperationException(
                $"Configuration value '{ConfigurationKey}' must be one of: " +
                $"{string.Join(", ", Enum.GetNames<AdapterMode>())}.");
        }

        return mode;
    }
}

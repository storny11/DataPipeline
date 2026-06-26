namespace DataRetriever.Api.Composition;

public enum AdapterMode
{
    Simulator,
    Real
}

public static class AdapterModeOptions
{
    public static AdapterMode FromConfiguration(IConfiguration configuration)
    {
        var configured = configuration["AdapterMode"];
        return Enum.TryParse<AdapterMode>(configured, ignoreCase: true, out var mode)
            ? mode
            : AdapterMode.Simulator;
    }
}

using DataRetriever.Api.Composition;
using Microsoft.Extensions.Configuration;

namespace DataRetriever.Tests.Api;

public sealed class AdapterModeConfigurationTests
{
    [Theory]
    [InlineData("Simulator", AdapterMode.Simulator)]
    [InlineData("real", AdapterMode.Real)]
    public void ReadRequired_WithDefinedMode_ReturnsMode(
        string configured,
        AdapterMode expected)
    {
        var configuration = BuildConfiguration(configured);

        var mode = AdapterModeConfiguration.ReadRequired(configuration);

        Assert.Equal(expected, mode);
    }

    [Fact]
    public void ReadRequired_WithoutValue_Throws()
    {
        var configuration = BuildConfiguration(null);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            AdapterModeConfiguration.ReadRequired(configuration));

        Assert.Contains(AdapterModeConfiguration.ConfigurationKey, exception.Message);
        Assert.Contains("missing", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadRequired_WithUnknownValue_Throws()
    {
        var configuration = BuildConfiguration("Unknown");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            AdapterModeConfiguration.ReadRequired(configuration));

        Assert.Contains(nameof(AdapterMode.Simulator), exception.Message);
        Assert.Contains(nameof(AdapterMode.Real), exception.Message);
    }

    private static IConfiguration BuildConfiguration(string? adapterMode)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [AdapterModeConfiguration.ConfigurationKey] = adapterMode
            })
            .Build();
    }
}

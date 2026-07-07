// Verifies API dependency composition choices for simulator and real adapter modes.
using DataRetriever.Api;
using DataRetriever.Api.Composition;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RunReporting;

namespace DataRetriever.Tests.Api;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddDataRetrieverApi_WithoutEmailReportSection_FailsFast()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new ConfigurationBuilder().Build();

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddDataRetrieverApi(configuration));

        Assert.Contains("EmailReport", exception.Message);
    }

    [Fact]
    public void AddDataRetrieverApi_WhenEmailReportEnabled_ConfiguresSmtpForLocalTesting()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EmailReport:Enabled"] = "true",
                ["EmailReport:Host"] = "localhost",
                ["EmailReport:Port"] = "2525",
                ["EmailReport:From"] = "dataretriever@test.local",
                ["EmailReport:To:0"] = "elena@test.local"
            })
            .Build();

        services.AddDataRetrieverApi(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<RunReportingOptions>();

        Assert.True(options.Enabled);
        Assert.Equal(2525, options.Port);
        Assert.Equal(["elena@test.local"], options.To);
    }

    [Fact]
    public void AddDataRetrieverApi_RealMode_FailsFastUntilRealAdaptersExist()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AdapterMode"] = AdapterMode.Real.ToString(),
                ["EmailReport:Enabled"] = "false"
            })
            .Build();

        var exception = Assert.Throws<NotSupportedException>(() => services.AddDataRetrieverApi(configuration));
        Assert.Contains("AdapterMode.Real is not available", exception.Message);
    }
}

// Verifies API dependency composition choices for simulator and real adapter modes.
using DataRetriever.Api;
using DataRetriever.Api.Composition;
using DataRetriever.Infrastructure.Reporting;
using DataRetriever.Reporting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DataRetriever.Tests.Api;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddDataRetrieverApi_DefaultSimulatorMode_UsesRealEmailPublisherForLocalSmtpTesting()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new ConfigurationBuilder().Build();

        services.AddDataRetrieverApi(configuration);

        using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IRunReportPublisher>();

        Assert.IsType<EmailRunReportPublisher>(publisher);
    }

    [Fact]
    public void AddDataRetrieverApi_RealMode_FailsFastUntilRealAdaptersExist()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AdapterMode"] = AdapterMode.Real.ToString()
            })
            .Build();

        var exception = Assert.Throws<NotSupportedException>(() => services.AddDataRetrieverApi(configuration));
        Assert.Contains("AdapterMode.Real is not available", exception.Message);
    }
}

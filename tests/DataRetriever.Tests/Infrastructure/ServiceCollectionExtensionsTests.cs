// Verifies unfinished real infrastructure registration fails fast instead of wiring placeholder adapters.
using DataRetriever.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DataRetriever.Tests.Infrastructure;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddDataRetrieverInfrastructure_FailsFastUntilRealAdaptersExist()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        var exception = Assert.Throws<NotSupportedException>(() =>
            services.AddDataRetrieverInfrastructure(configuration));

        Assert.Contains("real source and sink adapters are implemented", exception.Message);
    }
}

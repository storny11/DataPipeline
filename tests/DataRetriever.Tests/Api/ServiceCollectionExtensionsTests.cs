// Verifies API dependency composition choices for simulator and real adapter modes.
using DataRetriever.Api;
using DataRetriever.Api.Composition;
using DataRetriever.Api.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RunReporting;

namespace DataRetriever.Tests.Api;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddDataRetrieverApi_WithoutEmailReportSection_FailsFastAtComposition()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Application:Name"] = "Test application"
        });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddDataRetrieverApi(configuration, AdapterMode.Simulator));

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
                ["Application:Name"] = "Test application",
                ["EmailReport:Enabled"] = "true",
                ["EmailReport:Host"] = "localhost",
                ["EmailReport:Port"] = "2525",
                ["EmailReport:From"] = "dataretriever@test.local",
                ["EmailReport:ServiceName"] = "Configured data service",
                ["EmailReport:To"] = "elena@test.local; ops@test.local"
            })
            .Build();

        services.AddDataRetrieverApi(configuration, AdapterMode.Simulator);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<RunReportingOptions>();

        Assert.True(options.Enabled);
        Assert.Equal(2525, options.Port);
        Assert.Equal("Configured data service", options.ServiceName);
        Assert.Equal("elena@test.local; ops@test.local", options.To);
    }

    [Fact]
    public void AddDataRetrieverApi_RealMode_FailsFastUntilRealAdaptersExist()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Application:Name"] = "Test application",
                ["EmailReport:Enabled"] = "false"
            })
            .Build();

        var exception = Assert.Throws<NotSupportedException>(() =>
            services.AddDataRetrieverApi(configuration, AdapterMode.Real));
        Assert.Contains("AdapterMode.Real is not available", exception.Message);
    }

    [Fact]
    public async Task AddDataRetrieverApi_WithoutApplicationSection_FailsOnHostStartBeforeHostedWork()
    {
        var builder = CreateHostBuilder(new Dictionary<string, string?>
        {
            ["EmailReport:Enabled"] = "false"
        });
        var startupProbe = new StartupProbe();
        builder.Services.AddSingleton(startupProbe);
        builder.Services.AddHostedService(provider => provider.GetRequiredService<StartupProbe>());

        using var host = builder.Build();
        var exception = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());

        Assert.Contains(ApplicationOptions.SectionName, exception.Message);
        Assert.False(startupProbe.Started);
    }

    [Fact]
    public async Task AddDataRetrieverApi_WithInvalidApplicationName_FailsOnHostStart()
    {
        var builder = CreateHostBuilder(new Dictionary<string, string?>
        {
            ["Application:Name"] = " ",
            ["EmailReport:Enabled"] = "false"
        });

        using var host = builder.Build();
        var exception = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());

        Assert.Contains(nameof(ApplicationOptions.Name), exception.Message);
    }

    [Fact]
    public async Task AddDataRetrieverApi_WithValidConfiguration_StartsAndStopsHost()
    {
        var builder = CreateHostBuilder(ValidConfiguration());

        using var host = builder.Build();
        await host.StartAsync();

        var options = host.Services.GetRequiredService<IOptions<ApplicationOptions>>().Value;
        Assert.Equal("Test application", options.Name);

        await host.StopAsync();
    }

    [Fact]
    public void AddDataRetrieverApi_WithValidConfiguration_PassesStrictContainerValidation()
    {
        var builder = CreateHostBuilder(ValidConfiguration(), Environments.Development);

        using var host = builder.Build();

        Assert.NotNull(host.Services.GetRequiredService<IOptions<ApplicationOptions>>());
    }

    private static HostApplicationBuilder CreateHostBuilder(
        IReadOnlyDictionary<string, string?> values,
        string? environmentName = null)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = environmentName
        });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(values);
        builder.Logging.ClearProviders();
        builder.Services.AddDataRetrieverApi(builder.Configuration, AdapterMode.Simulator);
        return builder;
    }

    private static IConfiguration BuildConfiguration(IReadOnlyDictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static IReadOnlyDictionary<string, string?> ValidConfiguration()
    {
        return new Dictionary<string, string?>
        {
            ["Application:Name"] = "Test application",
            ["EmailReport:Enabled"] = "false"
        };
    }

    private sealed class StartupProbe : IHostedService
    {
        public bool Started { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            Started = true;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}

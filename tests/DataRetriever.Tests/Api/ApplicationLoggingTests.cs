using DataRetriever.Api.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace DataRetriever.Tests.Api;

public sealed class ApplicationLoggingTests
{
    [Fact]
    public void AddApplicationConfiguration_OverridesConfiguredFilePathWithResolvedPath()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Serilog:WriteTo:FileSink:Args:path"] = "configured-placeholder.log"
        });

        var launch = builder.AddApplicationConfiguration([]);

        Assert.Equal(
            launch.LogFilePath,
            builder.Configuration["Serilog:WriteTo:FileSink:Args:path"]);
    }

    [Fact]
    public void BootstrapAndFinalLoggers_UseTheSameResolvedConfiguredFile()
    {
        var testDirectory = Path.Combine(
            Path.GetTempPath(),
            "application-logging-tests",
            Guid.NewGuid().ToString("N"));
        var logFilePath = Path.Combine(testDirectory, "application.log");
        var configuration = CreateConfiguration();
        var bootstrapMarker = $"bootstrap-{Guid.NewGuid():N}";
        var finalMarker = $"final-{Guid.NewGuid():N}";

        try
        {
            var bootstrapLogger = ApplicationLogging.CreateFallbackBootstrapLogger();
            try
            {
                ApplicationLogging.ApplyResolvedLogFilePath(configuration, logFilePath);
                Assert.True(ApplicationLogging.TryConfigureBootstrapLogger(
                    bootstrapLogger,
                    configuration));

                bootstrapLogger.Information("{Marker}", bootstrapMarker);
            }
            finally
            {
                bootstrapLogger.Dispose();
            }

            using (var services = new ServiceCollection().BuildServiceProvider())
            using (var finalLogger = CreateFinalLogger(configuration, services))
            {
                finalLogger.Information("{Marker}", finalMarker);
            }

            var logContents = File.ReadAllText(logFilePath);
            Assert.Contains(bootstrapMarker, logContents, StringComparison.Ordinal);
            Assert.Contains(finalMarker, logContents, StringComparison.Ordinal);
            Assert.Equal(logFilePath, configuration["Serilog:WriteTo:FileSink:Args:path"]);
        }
        finally
        {
            if (Directory.Exists(testDirectory))
            {
                Directory.Delete(testDirectory, recursive: true);
            }
        }
    }

    private static ConfigurationManager CreateConfiguration()
    {
        var configuration = new ConfigurationManager();
        configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Serilog:Using:0"] = "Serilog.Sinks.File",
            ["Serilog:MinimumLevel:Default"] = "Information",
            ["Serilog:WriteTo:FileSink:Name"] = "File",
            ["Serilog:WriteTo:FileSink:Args:path"] = "placeholder.log",
            ["Serilog:WriteTo:FileSink:Args:shared"] = "true"
        });

        return configuration;
    }

    private static Serilog.Core.Logger CreateFinalLogger(
        IConfiguration configuration,
        IServiceProvider services)
    {
        var loggerConfiguration = new LoggerConfiguration();
        ApplicationLogging.ConfigureFinalLogger(
            services,
            loggerConfiguration,
            configuration,
            configurationLoggingAvailable: true);

        return loggerConfiguration.CreateLogger();
    }
}

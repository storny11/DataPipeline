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
    public void BootstrapFile_CapturesFailuresBeforeApplicationConfigurationExists()
    {
        var testDirectory = Path.Combine(
            Path.GetTempPath(),
            "application-logging-tests",
            Guid.NewGuid().ToString("N"));
        var logFilePath = Path.Combine(testDirectory, "application-.log");
        var marker = $"pre-configuration-{Guid.NewGuid():N}";

        try
        {
            var bootstrapLogger = ApplicationLogging.CreateBootstrapLogger();
            try
            {
                ApplicationLogging.EnableBootstrapFile(
                    bootstrapLogger,
                    logFilePath);
                bootstrapLogger.Fatal("{Marker}", marker);
            }
            finally
            {
                bootstrapLogger.Dispose();
            }

            var logContents = string.Join(
                Environment.NewLine,
                Directory.GetFiles(testDirectory).Select(File.ReadAllText));
            Assert.Contains(marker, logContents, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(testDirectory))
            {
                Directory.Delete(testDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void EnableBootstrapFile_WhenFileCannotBeOpened_Throws()
    {
        var blockingFilePath = Path.GetTempFileName();
        var bootstrapLogger = ApplicationLogging.CreateBootstrapLogger();

        try
        {
            Assert.ThrowsAny<IOException>(() => ApplicationLogging.EnableBootstrapFile(
                bootstrapLogger,
                Path.Combine(blockingFilePath, "application-.log")));
        }
        finally
        {
            bootstrapLogger.Dispose();
            File.Delete(blockingFilePath);
        }
    }

    [Fact]
    public void EnsureFileSinkIsConfigured_WhenNamedFileSinkIsMissing_Throws()
    {
        var configuration = new ConfigurationManager();

        Assert.Throws<InvalidOperationException>(
            () => ApplicationLogging.EnsureFileSinkIsConfigured(configuration));
    }

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

        var logFilePath = Path.GetFullPath("resolved.log");
        builder.AddApplicationConfiguration(
            [],
            logFilePath,
            ApplicationLaunchArguments.Parse(["--environment=local"]));

        Assert.Equal(
            logFilePath,
            builder.Configuration["Serilog:WriteTo:FileSink:Args:path"]);
    }

    [Fact]
    public void BootstrapAndFinalLoggers_UseTheSameResolvedConfiguredFile()
    {
        var testDirectory = Path.Combine(
            Path.GetTempPath(),
            "application-logging-tests",
            Guid.NewGuid().ToString("N"));
        var logFilePath = Path.Combine(testDirectory, "application-.log");
        var configuration = CreateConfiguration();
        var bootstrapMarker = $"bootstrap-{Guid.NewGuid():N}";
        var finalMarker = $"final-{Guid.NewGuid():N}";

        try
        {
            var bootstrapLogger = ApplicationLogging.CreateBootstrapLogger();
            try
            {
                ApplicationLogging.EnableBootstrapFile(bootstrapLogger, logFilePath);
                ApplicationLogging.ApplyResolvedLogFilePath(configuration, logFilePath);
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

            var logFiles = Directory.GetFiles(testDirectory);
            var logContents = string.Join(
                Environment.NewLine,
                logFiles.Select(File.ReadAllText));
            Assert.Single(logFiles);
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
            ["Serilog:WriteTo:FileSink:Args:rollingInterval"] = "Infinite",
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
            configuration);

        return loggerConfiguration.CreateLogger();
    }
}

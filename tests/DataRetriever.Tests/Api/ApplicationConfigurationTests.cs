using DataRetriever.Api.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace DataRetriever.Tests.Api;

public sealed class ApplicationConfigurationTests
{
    [Fact]
    public void AddApplicationConfiguration_AppliesDocumentedProviderPrecedence()
    {
        var contentRoot = CreateContentRoot(
            baseJson:
            """
            {
              "Layering": {
                "EnvironmentWins": "base",
                "ExternalWins": "base",
                "LocalWins": "base",
                "CommandLineWins": "base"
              }
            }
            """,
            environmentJson:
            """
            {
              "Layering": {
                "EnvironmentWins": "environment",
                "ExternalWins": "environment"
              }
            }
            """,
            localJson:
            """
            {
              "Layering": {
                "LocalWins": "local",
                "CommandLineWins": "local"
              }
            }
            """);
        var args = new[]
        {
            "--env=Development",
            "--externalProfile=remote-a",
            "--Layering:CommandLineWins=command-line",
            "--Serilog:WriteTo:FileSink:Args:path=untrusted.log"
        };

        try
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                Args = args,
                ContentRootPath = contentRoot,
                EnvironmentName = ApplicationEnvironment.ReadRequired(
                    args,
                    dotnetEnvironment: null,
                    aspNetCoreEnvironment: null)
            });
            string? bootstrapSelector = null;

            var launch = builder.AddApplicationConfiguration(
                args,
                configuration =>
                {
                    bootstrapSelector = configuration["externalProfile"];
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Layering:ExternalWins"] = "external",
                        ["Layering:LocalWins"] = "external"
                    });
                });

            Assert.Equal("remote-a", bootstrapSelector);
            Assert.Equal(Environments.Development, builder.Environment.EnvironmentName);
            Assert.Equal("environment", builder.Configuration["Layering:EnvironmentWins"]);
            Assert.Equal("external", builder.Configuration["Layering:ExternalWins"]);
            Assert.Equal("local", builder.Configuration["Layering:LocalWins"]);
            Assert.Equal("command-line", builder.Configuration["Layering:CommandLineWins"]);
            Assert.Equal(
                launch.LogFilePath,
                builder.Configuration["Serilog:WriteTo:FileSink:Args:path"]);
        }
        finally
        {
            Directory.Delete(contentRoot, recursive: true);
        }
    }

    [Fact]
    public void AddApplicationConfiguration_MissingLocalFileIsAllowed()
    {
        var contentRoot = CreateContentRoot(
            baseJson: "{}",
            environmentJson: "{}");

        try
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                Args = ["--env=Development"],
                ContentRootPath = contentRoot,
                EnvironmentName = Environments.Development
            });

            var launch = builder.AddApplicationConfiguration(["--env=Development"]);

            Assert.False(string.IsNullOrWhiteSpace(launch.LogFilePath));
        }
        finally
        {
            Directory.Delete(contentRoot, recursive: true);
        }
    }

    private static string CreateContentRoot(
        string baseJson,
        string environmentJson,
        string? localJson = null)
    {
        var contentRoot = Path.Combine(
            Path.GetTempPath(),
            "application-configuration-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(contentRoot);
        File.WriteAllText(Path.Combine(contentRoot, "appsettings.json"), baseJson);
        File.WriteAllText(
            Path.Combine(contentRoot, "appsettings.Development.json"),
            environmentJson);

        if (localJson is not null)
        {
            File.WriteAllText(Path.Combine(contentRoot, "appsettings.local.json"), localJson);
        }

        return contentRoot;
    }
}

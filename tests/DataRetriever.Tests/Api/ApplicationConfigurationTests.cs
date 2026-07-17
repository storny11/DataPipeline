using DataRetriever.Api.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DataRetriever.Tests.Api;

public sealed class ApplicationConfigurationTests
{
    [Fact]
    public void AddApplicationConfiguration_AppliesRequiredProviderPrecedence()
    {
        var contentRoot = CreateContentRoot(
            baseJson:
            """
            {
              "Application": { "Name": "base" },
              "Layering": {
                "ExternalOnly": "base",
                "EnvironmentWins": "base",
                "CommandLineWins": "base"
              }
            }
            """,
            environmentName: "local",
            environmentJson:
            """
            {
              "Application": { "Name": "environment" },
              "Layering": {
                "EnvironmentWins": "environment",
                "CommandLineWins": "environment"
              }
            }
            """);
        var args = new[]
        {
            "--environment=local",
            "--externalProfile=remote-a",
            "--application-name=command-line-name",
            "--adapter-mode=Real",
            "--log-level=Warning",
            "--Layering:CommandLineWins=command-line",
            "--Serilog:WriteTo:FileSink:Args:path=untrusted.log"
        };

        try
        {
            var launchArguments = ApplicationLaunchArguments.Parse(args);
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                Args = args,
                ContentRootPath = contentRoot,
                EnvironmentName = ApplicationEnvironment.ReadRequired(launchArguments)
            });
            string? selectedExternalProfile = null;

            var logFilePath = Path.Combine(contentRoot, "application.log");
            builder.AddApplicationConfiguration(
                args,
                logFilePath,
                launchArguments,
                (configuration, externalConfigurationProfile) =>
                {
                    selectedExternalProfile = externalConfigurationProfile;
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Layering:ExternalOnly"] = "external",
                        ["Layering:EnvironmentWins"] = "external",
                        ["Layering:CommandLineWins"] = "external"
                    });
                });

            Assert.Equal("remote-a", selectedExternalProfile);
            Assert.Equal("local", builder.Environment.EnvironmentName);
            Assert.Equal("external", builder.Configuration["Layering:ExternalOnly"]);
            Assert.Equal("environment", builder.Configuration["Layering:EnvironmentWins"]);
            Assert.Equal("command-line", builder.Configuration["Layering:CommandLineWins"]);
            Assert.Equal("command-line-name", builder.Configuration["Application:Name"]);
            Assert.Equal("Real", builder.Configuration["AdapterMode"]);
            Assert.Equal("Warning", builder.Configuration["Serilog:MinimumLevel:Default"]);
            Assert.Equal(
                logFilePath,
                builder.Configuration["Serilog:WriteTo:FileSink:Args:path"]);
        }
        finally
        {
            Directory.Delete(contentRoot, recursive: true);
        }
    }

    [Fact]
    public void AddApplicationConfiguration_MissingEnvironmentFileIsAllowed()
    {
        var contentRoot = CreateContentRoot(baseJson: "{}", environmentName: null, environmentJson: null);
        var args = new[] { "--environment=custom" };

        try
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                Args = args,
                ContentRootPath = contentRoot,
                EnvironmentName = "custom"
            });
            var launchArguments = ApplicationLaunchArguments.Parse(args);
            var logFilePath = Path.Combine(contentRoot, "application.log");

            builder.AddApplicationConfiguration(args, logFilePath, launchArguments);

            Assert.Equal(
                logFilePath,
                builder.Configuration["Serilog:WriteTo:FileSink:Args:path"]);
        }
        finally
        {
            Directory.Delete(contentRoot, recursive: true);
        }
    }

    [Fact]
    public void AddApplicationConfiguration_ExternalProviderRequiresStableProfile()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "local"
        });
        var launchArguments = ApplicationLaunchArguments.Parse(["--environment=local"]);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            builder.AddApplicationConfiguration(
                [],
                Path.GetFullPath("application.log"),
                launchArguments,
                (_, _) => { }));

        Assert.Contains("--externalProfile", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddApplicationConfiguration_ExternalProfileRequiresProvider()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "local"
        });
        var launchArguments = ApplicationLaunchArguments.Parse(
            ["--environment=local", "--externalProfile=remote-a"]);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            builder.AddApplicationConfiguration(
                [],
                Path.GetFullPath("application.log"),
                launchArguments));

        Assert.Contains("no external configuration provider", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConfigureApplicationHost_DoesNotEagerlyConstructEveryRegistration()
    {
        var builder = CreateBuilderWithRequiredLogging();
        builder.Services.AddSingleton<RequiresMissingDependency>();
        builder.ConfigureApplicationHost();

        using var app = builder.Build();

        Assert.NotNull(app.Services);
    }

    [Fact]
    public void ConfigureApplicationHost_AlwaysValidatesScopes()
    {
        var builder = CreateBuilderWithRequiredLogging();
        builder.Services.AddScoped<ScopedDependency>();
        builder.ConfigureApplicationHost();

        using var app = builder.Build();

        Assert.Throws<InvalidOperationException>(() =>
            app.Services.GetRequiredService<ScopedDependency>());
    }

    private static string CreateContentRoot(
        string baseJson,
        string? environmentName,
        string? environmentJson)
    {
        var contentRoot = Path.Combine(
            Path.GetTempPath(),
            "application-configuration-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(contentRoot);
        File.WriteAllText(Path.Combine(contentRoot, "appsettings.json"), baseJson);

        if (environmentName is not null && environmentJson is not null)
        {
            File.WriteAllText(
                Path.Combine(contentRoot, $"appsettings.{environmentName}.json"),
                environmentJson);
        }

        return contentRoot;
    }

    private static WebApplicationBuilder CreateBuilderWithRequiredLogging()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "custom"
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Serilog:WriteTo:FileSink:Name"] = "File",
            ["Serilog:WriteTo:FileSink:Args:path"] = Path.GetFullPath("application.log")
        });
        return builder;
    }

    private interface IMissingDependency
    {
    }

    private sealed class RequiresMissingDependency(IMissingDependency dependency)
    {
        public IMissingDependency Dependency { get; } = dependency;
    }

    private sealed class ScopedDependency
    {
    }
}

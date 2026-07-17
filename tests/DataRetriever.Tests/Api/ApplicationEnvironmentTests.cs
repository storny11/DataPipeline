using DataRetriever.Api.Hosting;
using Microsoft.Extensions.Hosting;

namespace DataRetriever.Tests.Api;

public sealed class ApplicationEnvironmentTests
{
    [Fact]
    public void Parse_CollectsBootstrapSelectorsWithLastValueWins()
    {
        var result = ApplicationLaunchArguments.Parse(
        [
            "--environment=Staging",
            "--instance=old",
            "--externalProfile=remote-a",
            "--instance=new"
        ]);

        Assert.Equal(Environments.Staging, result.Environment);
        Assert.Equal("new", result.Instance);
        Assert.Equal("remote-a", result.ExternalConfigurationProfile);
    }

    [Theory]
    [InlineData("--environment=Development")]
    [InlineData("environment=Development")]
    [InlineData("/environment=Development")]
    public void ReadRequired_UsesRequiredCommandLineEnvironment(string argument)
    {
        var launchArguments = ApplicationLaunchArguments.Parse([argument]);

        var result = ApplicationEnvironment.ReadRequired(launchArguments);

        Assert.Equal(Environments.Development, result);
    }

    [Fact]
    public void ReadRequired_UsesSeparatedCommandLineEnvironment()
    {
        var launchArguments = ApplicationLaunchArguments.Parse(
            ["--environment", Environments.Development]);

        var result = ApplicationEnvironment.ReadRequired(launchArguments);

        Assert.Equal(Environments.Development, result);
    }

    [Fact]
    public void ReadRequired_MissingEnvironmentFailsInsteadOfUsingFrameworkDefaults()
    {
        var launchArguments = ApplicationLaunchArguments.Parse([]);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ApplicationEnvironment.ReadRequired(launchArguments));

        Assert.Contains("--environment", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadRequired_LegacyEnvironmentAliasIsNotAccepted()
    {
        var launchArguments = ApplicationLaunchArguments.Parse(["--env=Development"]);

        Assert.Throws<InvalidOperationException>(() =>
            ApplicationEnvironment.ReadRequired(launchArguments));
    }

    [Theory]
    [InlineData("--environment=")]
    [InlineData("--environment= Development")]
    [InlineData("--environment=Dev Env")]
    [InlineData("--environment=Dev:Test")]
    [InlineData("--environment=../Development")]
    [InlineData("--environment=child/Development")]
    public void ReadRequired_UnsafeEnvironmentFails(string argument)
    {
        var launchArguments = ApplicationLaunchArguments.Parse([argument]);

        Assert.Throws<InvalidOperationException>(() =>
            ApplicationEnvironment.ReadRequired(launchArguments));
    }
}

using DataRetriever.Api.Hosting;
using Microsoft.Extensions.Hosting;

namespace DataRetriever.Tests.Api;

public sealed class ApplicationEnvironmentTests
{
    [Theory]
    [InlineData("--env=Development")]
    [InlineData("env=Development")]
    [InlineData("/env=Development")]
    public void ReadRequired_CommandLineEnvironmentWins(string argument)
    {
        var result = ApplicationEnvironment.ReadRequired(
            [argument],
            Environments.Staging,
            Environments.Production);

        Assert.Equal(Environments.Development, result);
    }

    [Fact]
    public void ReadRequired_SeparatedCommandLineEnvironmentWins()
    {
        var result = ApplicationEnvironment.ReadRequired(
            ["--env", Environments.Development],
            Environments.Staging,
            Environments.Production);

        Assert.Equal(Environments.Development, result);
    }

    [Fact]
    public void ReadRequired_DotnetEnvironmentWinsOverAspNetCoreEnvironment()
    {
        var result = ApplicationEnvironment.ReadRequired(
            [],
            Environments.Staging,
            Environments.Production);

        Assert.Equal(Environments.Staging, result);
    }

    [Fact]
    public void ReadRequired_UsesAspNetCoreEnvironmentAsLocalFallback()
    {
        var result = ApplicationEnvironment.ReadRequired(
            [],
            dotnetEnvironment: null,
            aspNetCoreEnvironment: Environments.Development);

        Assert.Equal(Environments.Development, result);
    }

    [Fact]
    public void ReadRequired_MissingEnvironmentFailsInsteadOfSilentlyUsingProduction()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ApplicationEnvironment.ReadRequired(
                [],
                dotnetEnvironment: null,
                aspNetCoreEnvironment: null));

        Assert.Contains("--env", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--env=")]
    [InlineData("--env= Development")]
    [InlineData("--env=Dev Env")]
    [InlineData("--env=Dev:Test")]
    [InlineData("--env=../Development")]
    [InlineData("--env=child/Development")]
    public void ReadRequired_UnsafeEnvironmentFails(string argument)
    {
        Assert.Throws<InvalidOperationException>(() =>
            ApplicationEnvironment.ReadRequired(
                [argument],
                dotnetEnvironment: null,
                aspNetCoreEnvironment: null));
    }
}

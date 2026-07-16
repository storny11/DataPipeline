using DataRetriever.Api.Hosting;
using Microsoft.Extensions.Hosting;

namespace DataRetriever.Tests.Api;

public sealed class ApplicationLogPathTests
{
    private static readonly string LogRoot = Path.GetFullPath(
        Path.Combine(Path.GetTempPath(), "application-log-path-tests"));

    [Fact]
    public void ResolveBootstrapFilePath_UsesTheSamePathAsFinalResolution()
    {
        var args = new[] { "--instance=worker-a" };

        var bootstrapPath = ApplicationLogPath.ResolveBootstrapFilePath(args);
        var finalPath = ApplicationLogPath.Resolve(args, Environments.Production).LogFilePath;

        Assert.Equal(finalPath, bootstrapPath);
    }

    [Fact]
    public void Resolve_DevelopmentWithoutInstance_UsesRootLogPath()
    {
        var result = Resolve([], Environments.Development);

        Assert.Equal(Path.Combine(LogRoot, "application-.log"), result.LogFilePath);
        Assert.Null(result.ValidationError);
    }

    [Fact]
    public void Resolve_ProductionWithoutInstance_UsesFallbackAndFailsValidation()
    {
        var result = Resolve([], Environments.Production);

        Assert.Equal(Path.Combine(LogRoot, "application-.log"), result.LogFilePath);
        Assert.Contains("required outside Development", result.ValidationError);
        Assert.Throws<InvalidOperationException>(result.EnsureLaunchIsValid);
    }

    [Theory]
    [InlineData("--instance=worker-a")]
    [InlineData("instance=worker-a")]
    [InlineData("/instance=worker-a")]
    public void Resolve_ProductionInstanceEqualsForms_UseInstanceLogPath(string argument)
    {
        var result = Resolve([argument], Environments.Production);

        Assert.Equal(
            Path.Combine(LogRoot, "worker-a", "application-.log"),
            result.LogFilePath);
        Assert.Null(result.ValidationError);
    }

    [Fact]
    public void Resolve_ProductionSeparatedInstance_UsesInstanceLogPath()
    {
        var result = Resolve(["--instance", "worker-a"], Environments.Production);

        Assert.Equal(
            Path.Combine(LogRoot, "worker-a", "application-.log"),
            result.LogFilePath);
        Assert.Null(result.ValidationError);
    }

    [Theory]
    [InlineData("--instance=")]
    [InlineData("--instance= ")]
    [InlineData("--instance=../outside")]
    [InlineData("--instance=child/path")]
    [InlineData("--instance=C:\\logs")]
    public void Resolve_UnsafeInstance_UsesFallbackAndFailsValidation(string argument)
    {
        var result = Resolve([argument], Environments.Production);

        Assert.Equal(Path.Combine(LogRoot, "application-.log"), result.LogFilePath);
        Assert.NotNull(result.ValidationError);
        Assert.Throws<InvalidOperationException>(result.EnsureLaunchIsValid);
    }

    [Fact]
    public void Resolve_LastInstanceArgumentWins()
    {
        var result = Resolve(
            ["--instance=old", "--instance=new"],
            Environments.Production);

        Assert.Equal(
            Path.Combine(LogRoot, "new", "application-.log"),
            result.LogFilePath);
        Assert.Null(result.ValidationError);
    }

    private static ApplicationLogPathResolution Resolve(string[] args, string environmentName)
    {
        return ApplicationLogPath.Resolve(
            args,
            environmentName,
            LogRoot,
            "application-.log");
    }
}

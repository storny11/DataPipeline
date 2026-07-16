using DataRetriever.Api.Hosting;
using Microsoft.Extensions.Hosting;

namespace DataRetriever.Tests.Api;

public sealed class ApplicationLogPathTests
{
    private static readonly string LogRoot = Path.GetFullPath(
        Path.Combine(Path.GetTempPath(), "application-log-path-tests"));

    [Fact]
    public void ResolveBootstrapFilePath_UsesInstanceLogPath()
    {
        var args = new[] { "--instance=worker-a" };

        var bootstrapPath = Resolve(args);

        Assert.Equal(Path.Combine(LogRoot, "worker-a", "application-.log"), bootstrapPath);
        ApplicationLogPath.EnsureLaunchIsValid(args, Environments.Production);
    }

    [Fact]
    public void Resolve_DevelopmentWithoutInstance_UsesRootLogPath()
    {
        var result = Resolve([]);

        Assert.Equal(Path.Combine(LogRoot, "application-.log"), result);
        ApplicationLogPath.EnsureLaunchIsValid([], Environments.Development);
    }

    [Fact]
    public void Resolve_ProductionWithoutInstance_UsesFallbackAndFailsValidation()
    {
        var result = Resolve([]);

        Assert.Equal(Path.Combine(LogRoot, "application-.log"), result);
        var exception = Assert.Throws<InvalidOperationException>(
            () => ApplicationLogPath.EnsureLaunchIsValid([], Environments.Production));
        Assert.Contains("required outside Development", exception.Message);
    }

    [Theory]
    [InlineData("--instance=worker-a")]
    [InlineData("instance=worker-a")]
    [InlineData("/instance=worker-a")]
    public void Resolve_ProductionInstanceEqualsForms_UseInstanceLogPath(string argument)
    {
        var result = Resolve([argument]);

        Assert.Equal(
            Path.Combine(LogRoot, "worker-a", "application-.log"),
            result);
        ApplicationLogPath.EnsureLaunchIsValid([argument], Environments.Production);
    }

    [Fact]
    public void Resolve_ProductionSeparatedInstance_UsesInstanceLogPath()
    {
        var args = new[] { "--instance", "worker-a" };
        var result = Resolve(args);

        Assert.Equal(
            Path.Combine(LogRoot, "worker-a", "application-.log"),
            result);
        ApplicationLogPath.EnsureLaunchIsValid(args, Environments.Production);
    }

    [Theory]
    [InlineData("--instance=")]
    [InlineData("--instance= ")]
    [InlineData("--instance=../outside")]
    [InlineData("--instance=child/path")]
    [InlineData("--instance=C:\\logs")]
    public void Resolve_UnsafeInstance_UsesFallbackAndFailsValidation(string argument)
    {
        var result = Resolve([argument]);

        Assert.Equal(Path.Combine(LogRoot, "application-.log"), result);
        Assert.Throws<InvalidOperationException>(
            () => ApplicationLogPath.EnsureLaunchIsValid([argument], Environments.Production));
    }

    [Fact]
    public void Resolve_LastInstanceArgumentWins()
    {
        var args = new[] { "--instance=old", "--instance=new" };
        var result = Resolve(args);

        Assert.Equal(
            Path.Combine(LogRoot, "new", "application-.log"),
            result);
        ApplicationLogPath.EnsureLaunchIsValid(args, Environments.Production);
    }

    private static string Resolve(string[] args)
    {
        return ApplicationLogPath.ResolveBootstrapFilePath(
            args,
            LogRoot,
            "application-.log");
    }
}

using DataRetriever.Api.Hosting;
using Microsoft.Extensions.Hosting;

namespace DataRetriever.Tests.Api;

public sealed class ApplicationLogPathTests
{
    private static readonly string LogRoot = Path.GetFullPath(
        Path.Combine(Path.GetTempPath(), "application-log-path-tests"));

    [Fact]
    public void Resolve_DevelopmentWithoutNamespace_UsesRootLogPath()
    {
        var result = Resolve([], Environments.Development);

        Assert.Equal(Path.Combine(LogRoot, "application-.log"), result.LogFilePath);
        Assert.Null(result.ValidationError);
    }

    [Fact]
    public void Resolve_ProductionWithoutNamespace_UsesFallbackAndFailsValidation()
    {
        var result = Resolve([], Environments.Production);

        Assert.Equal(Path.Combine(LogRoot, "application-.log"), result.LogFilePath);
        Assert.Contains("required outside Development", result.ValidationError);
        Assert.Throws<InvalidOperationException>(result.EnsureLaunchIsValid);
    }

    [Theory]
    [InlineData("--namespace=instance-a")]
    [InlineData("namespace=instance-a")]
    [InlineData("/namespace=instance-a")]
    public void Resolve_ProductionNamespaceEqualsForms_UseNamespacedLogPath(string argument)
    {
        var result = Resolve([argument], Environments.Production);

        Assert.Equal(
            Path.Combine(LogRoot, "instance-a", "application-.log"),
            result.LogFilePath);
        Assert.Null(result.ValidationError);
    }

    [Fact]
    public void Resolve_ProductionSeparatedNamespace_UsesNamespacedLogPath()
    {
        var result = Resolve(["--namespace", "instance-a"], Environments.Production);

        Assert.Equal(
            Path.Combine(LogRoot, "instance-a", "application-.log"),
            result.LogFilePath);
        Assert.Null(result.ValidationError);
    }

    [Theory]
    [InlineData("--namespace=")]
    [InlineData("--namespace= ")]
    [InlineData("--namespace=../outside")]
    [InlineData("--namespace=child/path")]
    [InlineData("--namespace=C:\\logs")]
    public void Resolve_UnsafeNamespace_UsesFallbackAndFailsValidation(string argument)
    {
        var result = Resolve([argument], Environments.Production);

        Assert.Equal(Path.Combine(LogRoot, "application-.log"), result.LogFilePath);
        Assert.NotNull(result.ValidationError);
        Assert.Throws<InvalidOperationException>(result.EnsureLaunchIsValid);
    }

    [Fact]
    public void Resolve_LastNamespaceArgumentWins()
    {
        var result = Resolve(
            ["--namespace=old", "--namespace=new"],
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

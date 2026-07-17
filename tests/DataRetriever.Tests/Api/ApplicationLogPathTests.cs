using DataRetriever.Api.Hosting;
using System.Globalization;

namespace DataRetriever.Tests.Api;

public sealed class ApplicationLogPathTests
{
    private static readonly string LogRoot = Path.GetFullPath(
        Path.Combine(Path.GetTempPath(), "application-log-path-tests"));

    [Fact]
    public void Resolve_ValidInstanceUsesInstanceSubfolder()
    {
        var result = ApplicationLogPath.Resolve(
            "worker-a",
            LogRoot,
            "application.log");

        Assert.Equal(Path.Combine(LogRoot, "worker-a", "application.log"), result.FilePath);
        result.ThrowIfInvalid();
    }

    [Fact]
    public void Resolve_MissingInstanceUsesRootAndIsValid()
    {
        var result = ApplicationLogPath.Resolve(
            instanceName: null,
            LogRoot,
            "application.log");

        Assert.Equal(Path.Combine(LogRoot, "application.log"), result.FilePath);
        result.ThrowIfInvalid();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("../outside")]
    [InlineData("child/path")]
    [InlineData("C:\\logs")]
    public void Resolve_UnsafeInstanceUsesDurableRootFallbackThenFails(string instanceName)
    {
        var result = ApplicationLogPath.Resolve(
            instanceName,
            LogRoot,
            "application.log");

        Assert.Equal(Path.Combine(LogRoot, "application.log"), result.FilePath);
        Assert.Throws<InvalidOperationException>(result.ThrowIfInvalid);
    }

    [Fact]
    public void Resolve_DefaultFileNameContainsApplicationDateAndProcessId()
    {
        var result = ApplicationLogPath.Resolve(instanceName: null);
        var expectedSuffix = $"_{DateTime.Today.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}_" +
                             $"{Environment.ProcessId.ToString(CultureInfo.InvariantCulture)}.log";

        Assert.StartsWith("DataRetriever.Api_", Path.GetFileName(result.FilePath), StringComparison.Ordinal);
        Assert.EndsWith(expectedSuffix, Path.GetFileName(result.FilePath), StringComparison.Ordinal);
        result.ThrowIfInvalid();
    }
}

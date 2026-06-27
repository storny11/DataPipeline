// Verifies the one-run-at-a-time guard behavior.
using DataRetriever.Application.Runs;

namespace DataRetriever.Tests.Runs;

public sealed class SingleRunGuardTests
{
    [Fact]
    public async Task TryEnterAsync_ReturnsNullWhileRunIsActive()
    {
        var guard = new SingleRunGuard();

        using var first = await guard.TryEnterAsync(CancellationToken.None);
        var second = await guard.TryEnterAsync(CancellationToken.None);

        Assert.NotNull(first);
        Assert.Null(second);
    }
}

using DataRetriever.Application.Step1Load.Models;
using DataRetriever.Application.Step2Load;
using DataRetriever.Application.Step2Load.Models;
using DataRetriever.Execution;

namespace DataRetriever.Tests.Application;

public sealed class Step2LoaderTests
{
    [Fact]
    public async Task ExecuteAsync_WhenSourceCancels_PropagatesCancellation()
    {
        var loader = new Step2Loader(
            new CancellingStep2SourceClient(),
            new Step2ResponseMapper(),
            new Step2Selector());

        await Assert.ThrowsAsync<OperationCanceledException>(() => loader.ExecuteAsync(
            new Step1Output([
                new Step1OutputRecord("INT-1", "EXT1-A", "GBP", 1)
            ]),
            new RunContext(Guid.NewGuid(), DateTimeOffset.UtcNow),
            CancellationToken.None));
    }

    private sealed class CancellingStep2SourceClient : IStep2SourceClient
    {
        public Task<IReadOnlyList<Step2ResponseDto>> FetchRelatedDataAsync(
            Step1OutputRecord input,
            CancellationToken cancellationToken)
        {
            throw new OperationCanceledException();
        }
    }
}

using DataRetriever.Application.Step2Load.Models;
using DataRetriever.Application.Step3Load;
using DataRetriever.Application.Step3Load.Models;

namespace DataRetriever.Tests.Application;

public sealed class Step3LoaderTests
{
    [Fact]
    public async Task ExecuteAsync_WithDuplicateInputRowsAndExtraResponseRows_CountsMatchedAndMissingRows()
    {
        var loader = new Step3Loader(
            new FakeStep3SourceClient(new Step3ResponseDto([
                new Step3ResponseItemDto("EXT2-A", "1.1", "2.2", "3.3"),
                new Step3ResponseItemDto("EXT2-EXTRA", "4.4", "5.5", "6.6")
            ])),
            new Step3RequestMapper(new ExternalId2Normalizer()),
            new Step3ResponseValidator(new ExternalId2Normalizer()),
            new Step3ResponseMapper(new ExternalId2Normalizer()),
            new ExternalId2Normalizer());

        var result = await loader.ExecuteAsync(
            new Step2Output([
                new Step2OutputRecord("INT-1", "EXT1-A", "EXT2-A", new DateOnly(2026, 6, 26)),
                new Step2OutputRecord("INT-2", "EXT1-B", "EXT2-A", new DateOnly(2026, 6, 26)),
                new Step2OutputRecord("INT-3", "EXT1-C", "EXT2-MISSING", new DateOnly(2026, 6, 26))
            ]),
            new DataRetriever.Execution.RunContext(Guid.NewGuid(), DateTimeOffset.UtcNow),
            CancellationToken.None);

        Assert.Equal(2, result.Output!.Records.Count);
        Assert.Equal(2, Counter(result.Counters, "RowsMatchedToStep2Output"));
        Assert.Equal(1, Counter(result.Counters, "MissingStep3Rows"));
    }

    private static long Counter(IEnumerable<DataRetriever.Execution.StepCounter> counters, string name)
    {
        return counters.Single(counter => counter.Name == name).Value;
    }

    private sealed class FakeStep3SourceClient(Step3ResponseDto response) : IStep3SourceClient
    {
        public Task<Step3ResponseDto> FetchAmountsAsync(
            Step3RequestDto request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(response);
        }
    }
}

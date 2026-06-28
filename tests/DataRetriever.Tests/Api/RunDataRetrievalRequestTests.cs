// Verifies API request parsing stays at the transport boundary.
using DataRetriever.Api.Contracts;

namespace DataRetriever.Tests.Api;

public sealed class RunDataRetrievalRequestTests
{
    [Fact]
    public void SplitInternalIds_WithCommaSeparatedString_ReturnsTrimmedValues()
    {
        var request = new RunDataRetrievalRequest(null, "BARC:L, DKE:DD,D:KKKX");

        var ids = request.SplitInternalIds();

        Assert.Equal(["BARC:L", "DKE:DD", "D:KKKX"], ids);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" ,, ")]
    public void SplitInternalIds_WithNoMeaningfulIds_ReturnsEmptyList(string? internalIds)
    {
        var request = new RunDataRetrievalRequest(null, internalIds);

        var ids = request.SplitInternalIds();

        Assert.Empty(ids);
    }
}

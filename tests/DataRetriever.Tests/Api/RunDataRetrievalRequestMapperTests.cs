// Verifies API request mapping stays at the transport boundary.
using DataRetriever.Api.Contracts;

namespace DataRetriever.Tests.Api;

public sealed class RunDataRetrievalRequestMapperTests
{
    [Fact]
    public void TryMap_WithCommaSeparatedInternalIds_ReturnsTrimmedApplicationOptions()
    {
        var request = new RunDataRetrievalRequest(null, "BARC:L, DKE:DD,D:KKKX");

        var isValid = RunDataRetrievalRequestMapper.TryMap(request, out var options, out var errorMessage);

        Assert.True(isValid);
        Assert.Null(errorMessage);
        Assert.Null(options.Currency);
        Assert.Equal(["BARC:L", "DKE:DD", "D:KKKX"], options.InternalIds);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" ,, ")]
    public void TryMap_WithNoMeaningfulInternalIds_ReturnsEmptyInternalIdList(string? internalIds)
    {
        var request = new RunDataRetrievalRequest(null, internalIds);

        var isValid = RunDataRetrievalRequestMapper.TryMap(request, out var options, out var errorMessage);

        Assert.True(isValid);
        Assert.Null(errorMessage);
        Assert.Empty(options.InternalIds);
    }

    [Fact]
    public void TryMap_WithCurrencyAndInternalIds_ReturnsValidationError()
    {
        var request = new RunDataRetrievalRequest("GBP", "BARC:L");

        var isValid = RunDataRetrievalRequestMapper.TryMap(request, out _, out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Provide either currency or internalIds, not both.", errorMessage);
    }
}

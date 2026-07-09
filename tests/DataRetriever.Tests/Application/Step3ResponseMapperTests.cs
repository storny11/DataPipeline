// Verifies Step 3 response amount parsing and duplicate row handling.
using System.Globalization;
using DataRetriever.Application.Step3Load;
using DataRetriever.Application.Step3Load.Models;
using Microsoft.Extensions.Logging.Abstractions;
using RunReporting;

namespace DataRetriever.Tests.Application;

public sealed class Step3ResponseMapperTests
{
    [Fact]
    public void Map_ParsesAmountsWithInvariantCulture()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            var reporter = new RunReporter(
                new RunReportingOptions(),
                [],
                NullLogger<RunReporter>.Instance);
            var normalizer = new ExternalId2Normalizer();
            var mapper = new Step3ResponseMapper(normalizer, reporter);
            normalizer.TryNormalize("EXT2-A", out var normalized);

            var amounts = mapper.Map(
                [new Step3ResponseItemDto("EXT2-A", "1.25", "2.50", "3.75")]);

            Assert.True(amounts.TryGetValue(normalized, out var mapped));
            Assert.Equal(1.25m, mapped.Amount1);
            Assert.Equal(2.50m, mapped.Amount2);
            Assert.Equal(3.75m, mapped.Amount3);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void Map_WhenDuplicateValidRowsExist_KeepsFirstAmountAndWarns()
    {
        var reporter = new RunReporter(
            new RunReportingOptions(),
            [],
            NullLogger<RunReporter>.Instance);
        var normalizer = new ExternalId2Normalizer();
        var mapper = new Step3ResponseMapper(normalizer, reporter);
        normalizer.TryNormalize("EXT2-A", out var normalized);

        var amounts = mapper.Map(
            [
                new Step3ResponseItemDto("EXT2-A", "1.25", "2.50", "3.75"),
                new Step3ResponseItemDto("EXT2-A", "9.99", "8.88", "7.77")
            ]);

        Assert.True(amounts.TryGetValue(normalized, out var mapped));
        Assert.Equal(1.25m, mapped.Amount1);
        Assert.Equal(2.50m, mapped.Amount2);
        Assert.Equal(3.75m, mapped.Amount3);

        var issue = Assert.Single(reporter.Take().Issues);
        Assert.Equal(IssueSeverity.Warning, issue.Severity);
        Assert.Equal("ExternalId2", issue.IdentifierName);
        Assert.Equal("EXT2-A", issue.IdentifierValue);
        Assert.Contains("more than one valid row", issue.Message, StringComparison.Ordinal);
    }
}

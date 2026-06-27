using System.Globalization;
using DataRetriever.Application.Step3Load;
using DataRetriever.Application.Step3Load.Models;
using DataRetriever.Execution;

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
            var normalizer = new ExternalId2Normalizer();
            var mapper = new Step3ResponseMapper(normalizer);
            normalizer.TryNormalize("EXT2-A", out var normalized);

            var result = mapper.Map(
                [new Step3ResponseItemDto("EXT2-A", "1.25", "2.50", "3.75")],
                new Dictionary<NormalizedExternalId2, DiagnosticContext>());

            Assert.True(result.Amounts.TryGetValue(normalized, out var amounts));
            Assert.Equal(1.25m, amounts.Amount1);
            Assert.Equal(2.50m, amounts.Amount2);
            Assert.Equal(3.75m, amounts.Amount3);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }
}

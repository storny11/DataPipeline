// Maps Step 1 source DTOs into internal output records and reports mapping issues.
using System.Globalization;
using DataRetriever.Application.Step1Load.Models;

namespace DataRetriever.Application.Step1Load;

public sealed class Step1Mapper
{
    public Step1OutputRecord Map(Step1SourceRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return new Step1OutputRecord(
            row.InternalId!.Trim(),
            row.ExternalId1!.Trim(),
            row.Currency!.Trim().ToUpperInvariant(),
            int.Parse(row.Step2RecordsToKeep!, NumberStyles.Integer, CultureInfo.InvariantCulture));
    }
}

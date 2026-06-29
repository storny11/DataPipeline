// Maps Step 1 source DTOs into internal output records and reports mapping issues.
using System.Globalization;
using DataRetriever.Application.Step1Load.Models;

namespace DataRetriever.Application.Step1Load;

public sealed class Step1Mapper
{
    public Step1OutputRecord Map(Step1Dto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new Step1OutputRecord(
            dto.InternalId!.Trim(),
            dto.ExternalId1!.Trim(),
            dto.Currency!.Trim().ToUpperInvariant(),
            int.Parse(dto.Step2RecordsToKeep!, NumberStyles.Integer, CultureInfo.InvariantCulture));
    }
}

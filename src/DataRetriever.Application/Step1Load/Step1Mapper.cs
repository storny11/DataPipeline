using DataRetriever.Application.Step1Load.Models;

namespace DataRetriever.Application.Step1Load;

public sealed class Step1Mapper
{
    public Step1OutputRecord Map(Step1Dto dto)
    {
        return new Step1OutputRecord(
            dto.InternalId!.Trim(),
            dto.ExternalId1!.Trim(),
            dto.Currency!.Trim().ToUpperInvariant(),
            dto.Step2RecordsToKeep);
    }
}

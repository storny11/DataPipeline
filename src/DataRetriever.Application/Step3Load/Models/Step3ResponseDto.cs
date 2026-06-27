// Represents the Step 3 source response envelope.
namespace DataRetriever.Application.Step3Load.Models;

public sealed record Step3ResponseDto(IReadOnlyList<Step3ResponseItemDto> Items);

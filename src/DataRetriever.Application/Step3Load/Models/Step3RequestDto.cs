// Represents the Step 3 request sent to the source client.
namespace DataRetriever.Application.Step3Load.Models;

public sealed record Step3RequestDto(IReadOnlyList<string> ExternalId2Values);

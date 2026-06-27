// Carries records confirmed as persisted by Step 4.
using DataRetriever.Application.Step3Load.Models;

namespace DataRetriever.Application.Step4Persist.Models;

public sealed record Step4Output(IReadOnlyList<Step3OutputRecord> PersistedRecords);

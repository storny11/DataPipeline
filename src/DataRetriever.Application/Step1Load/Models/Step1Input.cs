// Carries run options into Step 1.
using DataRetriever.Application.Runs;

namespace DataRetriever.Application.Step1Load.Models;

public sealed record Step1Input(DataRetrievalRunOptions RunOptions);

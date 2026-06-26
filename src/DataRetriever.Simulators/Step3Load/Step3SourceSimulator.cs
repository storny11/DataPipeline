using DataRetriever.Application.Step3Load;
using DataRetriever.Application.Step3Load.Models;

namespace DataRetriever.Simulators.Step3Load;

public sealed class Step3SourceSimulator(SimulatorSeedData seedData) : IStep3SourceClient
{
    public Task<Step3ResponseDto> FetchAmountsAsync(
        Step3RequestDto request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (request.ExternalId2Values.Contains("EXT2-THROW", StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Simulated Step 3 source failure.");
        }

        var rows = request.ExternalId2Values
            .Where(value => seedData.Step3Rows.ContainsKey(value))
            .Select(value => seedData.Step3Rows[value])
            .ToList();

        return Task.FromResult(new Step3ResponseDto(rows));
    }
}

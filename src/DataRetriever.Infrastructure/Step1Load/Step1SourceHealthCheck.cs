using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DataRetriever.Infrastructure.Step1Load;

public sealed class Step1SourceHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(HealthCheckResult.Unhealthy("Step 1 real source adapter is not implemented."));
    }
}

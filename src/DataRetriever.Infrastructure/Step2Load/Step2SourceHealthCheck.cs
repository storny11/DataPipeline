using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DataRetriever.Infrastructure.Step2Load;

public sealed class Step2SourceHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(HealthCheckResult.Unhealthy("Step 2 real source adapter is not implemented."));
    }
}

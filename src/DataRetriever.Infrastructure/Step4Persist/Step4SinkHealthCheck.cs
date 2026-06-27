// Reports Step 4 real sink readiness to health checks.
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DataRetriever.Infrastructure.Step4Persist;

public sealed class Step4SinkHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(HealthCheckResult.Unhealthy("Step 4 real sink adapter is not implemented."));
    }
}

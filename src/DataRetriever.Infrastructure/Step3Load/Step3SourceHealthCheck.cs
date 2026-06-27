// Reports Step 3 real adapter readiness to health checks.
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DataRetriever.Infrastructure.Step3Load;

public sealed class Step3SourceHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(HealthCheckResult.Unhealthy("Step 3 real source adapter is not implemented."));
    }
}

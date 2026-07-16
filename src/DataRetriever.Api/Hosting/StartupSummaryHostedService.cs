// Logs only explicitly safe startup metadata after all ValidateOnStart checks have passed.
using DataRetriever.Api.Configuration;
using Microsoft.Extensions.Options;

namespace DataRetriever.Api.Hosting;

internal sealed class StartupSummaryHostedService(
    IOptions<ApplicationOptions> options,
    IHostEnvironment environment,
    ILogger<StartupSummaryHostedService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Starting {ApplicationName} in {EnvironmentName}.",
            options.Value.Name,
            environment.EnvironmentName);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

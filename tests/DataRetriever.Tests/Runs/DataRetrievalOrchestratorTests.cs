using DataRetriever.Application;
using DataRetriever.Application.Runs;
using DataRetriever.Execution;
using DataRetriever.Monitoring;
using DataRetriever.Reporting;
using DataRetriever.Simulators;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DataRetriever.Tests.Runs;

public sealed class DataRetrievalOrchestratorTests
{
    [Fact]
    public async Task RunAsync_WithSimulatorData_ReturnsWarningsAndPersistedRows()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services
            .AddDataRetrieverExecution()
            .AddDataRetrieverReporting()
            .AddDataRetrieverMonitoring()
            .AddDataRetrieverApplication()
            .AddDataRetrieverSimulators();

        await using var provider = services.BuildServiceProvider();
        var orchestrator = provider.GetRequiredService<DataRetrievalOrchestrator>();

        var report = await orchestrator.RunAsync(DataRetrievalRunOptions.All, CancellationToken.None);
        var tracker = provider.GetRequiredService<IProcessingTracker>();
        var snapshot = await tracker.GetSnapshotAsync(report.RunId, CancellationToken.None);

        Assert.Equal("Success", report.Status);
        Assert.NotEmpty(report.Issues);
        Assert.True(report.Summary.WarningCount > 0);
        Assert.NotEmpty(report.PersistedRecords);
        Assert.All(report.PersistedRecords, record => Assert.False(string.IsNullOrWhiteSpace(record.InternalId)));
        Assert.NotNull(snapshot);
        Assert.Equal(ProcessingRunStatus.Success, snapshot.RunStatus);
    }
}

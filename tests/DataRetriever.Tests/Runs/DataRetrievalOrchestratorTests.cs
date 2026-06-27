// Verifies the full simulator-backed orchestration path and status tracking.
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
            .AddDataRetrieverReporting()
            .AddDataRetrieverMonitoring()
            .AddDataRetrieverApplication()
            .AddDataRetrieverSimulators();

        await using var provider = services.BuildServiceProvider();
        var orchestrator = provider.GetRequiredService<DataRetrievalOrchestrator>();

        var report = await orchestrator.RunAsync(DataRetrievalRunOptions.All, CancellationToken.None);
        var tracker = provider.GetRequiredService<IProcessingTracker>();
        var snapshot = await tracker.GetSnapshotAsync(report.RunId, CancellationToken.None);

        Assert.Equal(RunStatus.Success, report.Status);
        Assert.NotEmpty(report.Issues);
        Assert.True(report.WarningCount > 0);

        var persistedRecordsTable = Assert.Single(report.Tables, table => table.Name == "persisted-records");
        Assert.NotEmpty(persistedRecordsTable.Rows);
        Assert.All(
            persistedRecordsTable.Rows,
            row => Assert.False(string.IsNullOrWhiteSpace(row["internalId"])));

        Assert.NotNull(snapshot);
        Assert.Equal(RunStatus.Success, snapshot.RunStatus);
    }
}

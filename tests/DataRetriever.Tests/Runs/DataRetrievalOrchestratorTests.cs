// Verifies the full simulator-backed orchestration path, the published report, and status tracking.
using DataRetriever.Application;
using DataRetriever.Application.Runs;
using DataRetriever.Execution;
using DataRetriever.Monitoring;
using DataRetriever.Simulators;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RunReporting;

namespace DataRetriever.Tests.Runs;

public sealed class DataRetrievalOrchestratorTests
{
    [Fact]
    public async Task RunAsync_WithSimulatorData_PublishesWarningsAndPersistedRows()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var publisher = new CapturingPublisher();
        services.AddSingleton<IRunReportPublisher>(publisher);
        services.AddRunReporting(options => options.Enabled = false);
        services
            .AddDataRetrieverMonitoring()
            .AddDataRetrieverApplication()
            .AddDataRetrieverSimulators();

        await using var provider = services.BuildServiceProvider();
        var orchestrator = provider.GetRequiredService<DataRetrievalOrchestrator>();

        var result = await orchestrator.RunAsync(DataRetrievalRunOptions.All, CancellationToken.None);
        var tracker = provider.GetRequiredService<IProcessingTracker>();
        var snapshot = await tracker.GetSnapshotAsync(result.RunId, CancellationToken.None);

        Assert.Equal(RunStatus.Success, result.Status);

        var report = Assert.Single(publisher.Published);
        Assert.Equal(result.RunId.ToString(), report.Attributes["runId"]);
        Assert.Equal(RunOutcome.CompletedWithWarnings, report.Outcome);
        Assert.True(report.WarningCount > 0);

        var persistedTable = Assert.Single(report.Tables, table => table.Title == "Persisted Records");
        Assert.Equal("INTERNAL ID", persistedTable.Headers[0]);
        Assert.NotEmpty(persistedTable.Rows);
        Assert.All(persistedTable.Rows, row => Assert.False(string.IsNullOrWhiteSpace(row[0])));

        Assert.NotNull(snapshot);
        Assert.Equal(RunStatus.Success, snapshot.RunStatus);
    }

    private sealed class CapturingPublisher : IRunReportPublisher
    {
        public List<RunReport> Published { get; } = [];

        public Task PublishAsync(RunReport report, CancellationToken cancellationToken)
        {
            Published.Add(report);
            return Task.CompletedTask;
        }
    }
}

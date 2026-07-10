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

        using var requestCancellation = new CancellationTokenSource();
        var result = await orchestrator.RunAsync(DataRetrievalRunOptions.All, requestCancellation.Token);
        var tracker = provider.GetRequiredService<IProcessingTracker>();
        var snapshot = await tracker.GetSnapshotAsync(result.RunId, CancellationToken.None);

        Assert.Equal(RunStatus.Success, result.Status);

        var report = Assert.Single(publisher.Published);
        Assert.False(Assert.Single(publisher.PublicationTokens).CanBeCanceled);
        Assert.Equal(result.RunId.ToString(), report.Attributes["runId"]);
        Assert.Contains(report.Attributes.Keys, key => key.Equals("started (ET)", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Attributes.Keys, key => key.Equals("completed (ET)", StringComparison.OrdinalIgnoreCase));
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

        public List<CancellationToken> PublicationTokens { get; } = [];

        public Task PublishAsync(RunReport report, CancellationToken cancellationToken)
        {
            Published.Add(report);
            PublicationTokens.Add(cancellationToken);
            return Task.CompletedTask;
        }
    }
}

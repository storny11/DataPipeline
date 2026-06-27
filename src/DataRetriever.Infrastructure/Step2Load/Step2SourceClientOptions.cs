// Binds configuration for a future real Step 2 source client.
namespace DataRetriever.Infrastructure.Step2Load;

public sealed class Step2SourceClientOptions
{
    public string? ConnectionString { get; init; }

    public string? BaseUrl { get; init; }

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
}

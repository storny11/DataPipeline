namespace DataRetriever.Infrastructure.Step3Load;

public sealed class Step3SourceClientOptions
{
    public string BaseUrl { get; init; } = "";

    public TimeSpan PerTryTimeout { get; init; } = TimeSpan.FromSeconds(30);

    public int MaxRetryAttempts { get; init; } = 3;

    public TimeSpan RetryBaseDelay { get; init; } = TimeSpan.FromMilliseconds(200);
}

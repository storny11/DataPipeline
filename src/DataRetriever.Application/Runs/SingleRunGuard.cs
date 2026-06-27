// Prevents overlapping runs in this prototype host.
namespace DataRetriever.Application.Runs;

public sealed class SingleRunGuard
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task<RunLease?> TryEnterAsync(CancellationToken cancellationToken)
    {
        var acquired = await _semaphore.WaitAsync(0, cancellationToken);
        return acquired ? new RunLease(_semaphore) : null;
    }

    public sealed class RunLease(SemaphoreSlim semaphore) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            semaphore.Release();
            _disposed = true;
        }
    }
}

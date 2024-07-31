namespace Orc.Monitoring.Reporters;

using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

public sealed class AsyncCompositeDisposable : IAsyncDisposable
{
    private readonly ConcurrentQueue<IAsyncDisposable> _disposables = new();

    public void Add(IAsyncDisposable disposable)
    {
        _disposables.Enqueue(disposable);
    }

    public async ValueTask DisposeAsync()
    {
        while (_disposables is { Count: > 0 })
        {
            _disposables.TryDequeue(out var disposable);
            if (disposable is not null)
            {
                await disposable.DisposeAsync();
            }
        }
    }
}

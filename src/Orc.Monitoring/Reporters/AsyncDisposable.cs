namespace Orc.Monitoring.Reporters;

using System;
using System.Threading.Tasks;

public sealed class AsyncDisposable : IAsyncDisposable
{
    private readonly Func<ValueTask> _disposeAsync;

    public AsyncDisposable(Func<ValueTask> disposeAsync)
    {
        _disposeAsync = disposeAsync;
    }

    public ValueTask DisposeAsync()
    {
        return _disposeAsync();
    }

    public static IAsyncDisposable Empty { get; } = new AsyncDisposable(() => new ValueTask());
}

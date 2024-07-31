namespace Orc.Monitoring.Reporters;

using System;
using System.Threading.Tasks;

public sealed class AsyncDisposable : IAsyncDisposable
{
    private readonly Func<Task> _disposeAction;

    public AsyncDisposable(Func<Task> disposeAction)
    {
        _disposeAction = disposeAction;
    }

    public async ValueTask DisposeAsync()
    {
        await _disposeAction();
    }
}
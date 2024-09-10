namespace Orc.Monitoring.Reporters;

using System;
using System.Threading.Tasks;

public sealed class AsyncDisposable(Func<Task> disposeAction) : IAsyncDisposable
{
    public async ValueTask DisposeAsync()
    {
        await disposeAction();
    }
}

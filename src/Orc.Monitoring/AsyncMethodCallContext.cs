namespace Orc.Monitoring;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public sealed class AsyncMethodCallContext : MethodCallContextBase, IAsyncDisposable
{
    public static AsyncMethodCallContext Dummy { get; } = new AsyncMethodCallContext();

    private AsyncMethodCallContext() : base(null, null, null) { }

    public AsyncMethodCallContext(IClassMonitor? classMonitor, MethodCallInfo methodCallInfo, List<IAsyncDisposable> disposables)
        : base(classMonitor, methodCallInfo, disposables) { }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed || MethodCallInfo is null || _disposables is null)
        {
            return;
        }

        Console.WriteLine($"AsyncMethodCallContext.DisposeAsync called for {MethodCallInfo}");
        LogEnd();

        foreach (var disposable in _disposables)
        {
            await disposable.DisposeAsync();
        }

        MethodCallInfo.TryReturnToPool();
        _isDisposed = true;
    }
}

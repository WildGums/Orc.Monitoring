namespace Orc.Monitoring;

using System;
using System.Collections.Generic;

public sealed class MethodCallContext : MethodCallContextBase, IDisposable
{
    public static MethodCallContext Dummy { get; } = new MethodCallContext();

    private MethodCallContext() : base(null, null, null) { }

    public MethodCallContext(IClassMonitor? classMonitor, MethodCallInfo methodCallInfo, List<IAsyncDisposable> disposables)
        : base(classMonitor, methodCallInfo, disposables) { }

    public void Dispose()
    {
        if (_isDisposed || MethodCallInfo is null)
        {
            return;
        }

        Console.WriteLine($"MethodCallContext.Dispose called for {MethodCallInfo}");
        LogEnd();

        foreach (var disposable in _disposables ?? [])
        {
            disposable.DisposeAsync().AsTask().Wait(1000);
        }

        MethodCallInfo.TryReturnToPool();
        _isDisposed = true;
    }
}


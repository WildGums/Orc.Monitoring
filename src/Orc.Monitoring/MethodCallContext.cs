namespace Orc.Monitoring;

using System;
using System.Collections.Generic;

public sealed class MethodCallContext : MethodCallContextBase, IDisposable
{
    public MethodCallContext(IClassMonitor? classMonitor, MethodCallInfo methodCallInfo, List<IAsyncDisposable> disposables)
        : base(classMonitor, methodCallInfo, disposables) { }

    public MethodCallContext()
    {

    }

#pragma warning disable IDISP012
    public static MethodCallContext Dummy => new();
#pragma warning restore IDISP012

    public void Dispose()
    {
        if (_isDisposed || MethodCallInfo is null)
        {
            return;
        }

        LogEnd();

        foreach (var disposable in _disposables ?? [])
        {
            disposable.DisposeAsync().AsTask().Wait(1000);
        }

        MethodCallInfo.TryReturnToPool();
        _isDisposed = true;
    }
}


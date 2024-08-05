namespace Orc.Monitoring;

using System;
using System.Collections.Generic;
using MethodLifeCycleItems;

public abstract class MethodCallContextBase
{
    protected readonly IClassMonitor? _classMonitor;
    protected readonly List<IAsyncDisposable>? _disposables;
    protected readonly System.Diagnostics.Stopwatch _stopwatch = new();
    protected bool _isDisposed;

    protected MethodCallContextBase(IClassMonitor? classMonitor, MethodCallInfo? methodCallInfo, List<IAsyncDisposable>? disposables)
    {
        _classMonitor = classMonitor;
        _disposables = disposables;
        MethodCallInfo = methodCallInfo;
        _stopwatch.Start();

        // Log the start of the method only if we have a valid MethodCallInfo
        if (methodCallInfo is not null)
        {
            var startStatus = new MethodCallStart(methodCallInfo);
            (_classMonitor as ClassMonitor)?.LogStatus(startStatus);
        }
    }

    public MethodCallInfo? MethodCallInfo { get; }

    public void LogException(Exception exception)
    {
        if (MethodCallInfo is null)
        {
            return;
        }

        var exceptionStatus = new MethodCallException(MethodCallInfo, exception);
        (_classMonitor as ClassMonitor)?.LogStatus(exceptionStatus);
    }

    public void Log(string category, object data)
    {
        if (MethodCallInfo is null)
        {
            return;
        }

        var logEntry = new LogEntryItem(MethodCallInfo, category, data);
        (_classMonitor as ClassMonitor)?.LogStatus(logEntry);
    }

    public void SetParameter(string name, string value)
    {
        if (MethodCallInfo is null || MethodCallInfo.Parameters is null)
        {
            return;
        }

        MethodCallInfo.Parameters[name] = value;
    }

    protected void LogEnd()
    {
        if (MethodCallInfo is null)
        {
            return;
        }

        Console.WriteLine($"MethodCallContextBase.LogEnd called for {MethodCallInfo}");
        _stopwatch.Stop();
        MethodCallInfo.Elapsed = _stopwatch.Elapsed;
        var endStatus = new MethodCallEnd(MethodCallInfo);
        if (MonitoringController.ShouldTrack(MethodCallInfo.MonitoringVersion))
        {
            (_classMonitor as ClassMonitor)?.LogStatus(endStatus);
        }
        else
        {
            Console.WriteLine($"Skipping end logging for {MethodCallInfo} due to monitoring state or version mismatch");
        }
    }
}

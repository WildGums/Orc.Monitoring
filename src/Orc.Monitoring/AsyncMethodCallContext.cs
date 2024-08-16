﻿namespace Orc.Monitoring;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MethodLifeCycleItems;
using Microsoft.Extensions.Logging;


public sealed class AsyncMethodCallContext : VersionedMonitoringContext, IAsyncDisposable
{
    private readonly IClassMonitor? _classMonitor;
    private readonly List<IAsyncDisposable>? _disposables;
    private readonly System.Diagnostics.Stopwatch _stopwatch = new();
    private bool _isDisposed;

    public static AsyncMethodCallContext Dummy { get; } = new AsyncMethodCallContext();

    public MethodCallInfo? MethodCallInfo { get; }

    private AsyncMethodCallContext() 
    {
        // Dummy constructor
    }

    public AsyncMethodCallContext(IClassMonitor? classMonitor, MethodCallInfo methodCallInfo, List<IAsyncDisposable> disposables)
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

    public void LogException(Exception exception)
    {
        if (MethodCallInfo?.IsNull ?? true)
        {
            // Do nothing if we're using a dummy context
            return;
        }

        try
        {
            EnsureValidVersion();
            if (MethodCallInfo is null)
            {
                return;
            }

            var exceptionStatus = new MethodCallException(MethodCallInfo, exception);
            (_classMonitor as ClassMonitor)?.LogStatus(exceptionStatus);
        }
        catch (InvalidOperationException)
        {
            // Silently ignore version mismatch when monitoring is not properly configured
        }
    }

    public void Log(string category, object data)
    {
        EnsureValidVersion();
        if (MethodCallInfo is null)
        {
            return;
        }

        var logEntry = new LogEntryItem(MethodCallInfo, category, data);
        (_classMonitor as ClassMonitor)?.LogStatus(logEntry);
    }

    public void SetParameter(string name, string value)
    {
        EnsureValidVersion();
        if (MethodCallInfo is null || MethodCallInfo.Parameters is null)
        {
            return;
        }

        MethodCallInfo.Parameters[name] = value;
    }

    protected override void OnVersionUpdated()
    {
        // Handle version update if necessary
        // For example, you might want to log this event
        Log("VersionUpdate", $"Context updated to version {ContextVersion}");
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed || MethodCallInfo is null)
        {
            return;
        }

        try
        {
            EnsureValidVersion();

            _stopwatch.Stop();
            MethodCallInfo.Elapsed = _stopwatch.Elapsed;
            var endStatus = new MethodCallEnd(MethodCallInfo);

            if (MonitoringController.ShouldTrack(ContextVersion))
            {
                (_classMonitor as ClassMonitor)?.LogStatus(endStatus);
            }
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("outdated version"))
        {
            // Log that the context was operating under an outdated version
            MonitoringController.CreateLogger<AsyncMethodCallContext>().LogWarning(
                $"Async method call context disposed with outdated version. Method: {MethodCallInfo.MethodName}, Context Version: {ContextVersion}, Current Version: {MonitoringController.GetCurrentVersion()}");
        }
        finally
        {
            if (_disposables is not null)
            {
                foreach (var disposable in _disposables)
                {
                    try
                    {
                        await disposable.DisposeAsync();
                    }
                    catch (Exception ex)
                    {
                        MonitoringController.CreateLogger<AsyncMethodCallContext>().LogError(ex, "Error disposing async disposable");
                    }
                }
            }

            MethodCallInfo.TryReturnToPool();
            _isDisposed = true;
        }
    }
}

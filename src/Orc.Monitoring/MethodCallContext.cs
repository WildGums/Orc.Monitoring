namespace Orc.Monitoring;

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using MethodLifeCycleItems;


public sealed class MethodCallContext : VersionedMonitoringContext, IDisposable
{
    private readonly IClassMonitor? _classMonitor;
    private readonly List<IAsyncDisposable>? _disposables;
    private readonly System.Diagnostics.Stopwatch _stopwatch = new();
    private bool _isDisposed;

    public static MethodCallContext Dummy { get; } = new MethodCallContext();

    public MethodCallInfo? MethodCallInfo { get; }

    private MethodCallContext() : base()
    {
        // Dummy constructor
    }

    public MethodCallContext(IClassMonitor? classMonitor, MethodCallInfo methodCallInfo, List<IAsyncDisposable> disposables)
        : base()
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
        EnsureValidVersion();
        if (MethodCallInfo is null)
        {
            return;
        }

        var exceptionStatus = new MethodCallException(MethodCallInfo, exception);
        (_classMonitor as ClassMonitor)?.LogStatus(exceptionStatus);
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

    public void Dispose()
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
            MonitoringController.CreateLogger<MethodCallContext>().LogWarning(
                $"Method call context disposed with outdated version. Method: {MethodCallInfo.MethodName}, Context Version: {ContextVersion}, Current Version: {MonitoringController.GetCurrentVersion()}");
        }
        finally
        {
            foreach (var disposable in _disposables ?? [])
            {
                try
                {
                    disposable.DisposeAsync().AsTask().Wait(1000);
                }
                catch (Exception ex)
                {
                    MonitoringController.CreateLogger<MethodCallContext>().LogError(ex, "Error disposing async disposable");
                }
            }

            MethodCallInfo.TryReturnToPool();
            _isDisposed = true;
        }
    }
}

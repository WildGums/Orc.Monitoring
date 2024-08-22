﻿namespace Orc.Monitoring;

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using MethodLifeCycleItems;


public sealed class MethodCallContext : VersionedMonitoringContext, IDisposable
{
    private readonly ILogger<MethodCallContext> _logger = MonitoringController.CreateLogger<MethodCallContext>();

    private readonly IClassMonitor? _classMonitor;
    private readonly List<IAsyncDisposable>? _disposables;
    private readonly System.Diagnostics.Stopwatch _stopwatch = new();
    private bool _isDisposed;

    public static MethodCallContext Dummy { get; } = new();

    public MethodCallInfo? MethodCallInfo { get; }

    public IReadOnlyList<string> ReporterIds { get; }

    private MethodCallContext() : base()
    {
        // Dummy constructor
        ReporterIds = Array.Empty<string>();
    }

    public MethodCallContext(IClassMonitor? classMonitor, MethodCallInfo methodCallInfo, List<IAsyncDisposable> disposables, IEnumerable<string> reporterIds)
        : base()
    {
        _classMonitor = classMonitor;
        _disposables = disposables;
        MethodCallInfo = methodCallInfo;
        ReporterIds = reporterIds.ToList().AsReadOnly();

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

            var logEntry = new LogEntryItem(MethodCallInfo, category, data);
            (_classMonitor as ClassMonitor)?.LogStatus(logEntry);
        }
        catch (InvalidOperationException)
        {
            // Silently ignore version mismatch when monitoring is not properly configured
        }
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
        // For example, we might want to log this event
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

            if (MonitoringController.ShouldTrack(ContextVersion, reporterIds: ReporterIds))
            {
                (_classMonitor as ClassMonitor)?.LogStatus(endStatus);
            }
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("outdated version"))
        {
            // Log that the context was operating under an outdated version
            _logger.LogWarning(
                $"Method call context disposed with outdated version. Method: {MethodCallInfo.MethodName}, ReporterId: {ReporterIds.FirstOrDefault()}, Context Version: {ContextVersion}, Current Version: {MonitoringController.GetCurrentVersion()}");
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
            _logger.LogInformation($"MethodCallContext disposed at {DateTime.Now:HH:mm:ss.fff}");
            _isDisposed = true;
        }
    }
}

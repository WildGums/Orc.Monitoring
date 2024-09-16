﻿namespace Orc.Monitoring;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MethodLifeCycleItems;
using Microsoft.Extensions.Logging;

public sealed class AsyncMethodCallContext : MethodCallContextBase
{
    public AsyncMethodCallContext(
        IClassMonitor? classMonitor,
        MethodCallInfo methodCallInfo,
        List<IAsyncDisposable> disposables,
        IEnumerable<string> reporterIds,
        IMonitoringLoggerFactory loggerFactory,
        IMonitoringController monitoringController,
        MethodCallInfoPool methodCallInfoPool)
        : base(classMonitor, methodCallInfo, disposables, reporterIds, loggerFactory.CreateLogger<AsyncMethodCallContext>(), monitoringController, methodCallInfoPool)
    {
    }

    /// <summary>
    /// Logs an exception that occurred during async method execution.
    /// </summary>
    /// <param name="exception">The exception to log.</param>
    public override void LogException(Exception exception)
    {
        // Implementation...
    }

    /// <summary>
    /// Logs custom data during async method execution.
    /// </summary>
    /// <param name="category">The category of the log entry.</param>
    /// <param name="data">The data to log.</param>
    public override void Log(string category, object data)
    {
        // Implementation...
    }

    /// <summary>
    /// Sets a parameter value for the async method call.
    /// </summary>
    /// <param name="name">The name of the parameter.</param>
    /// <param name="value">The value of the parameter.</param>
    public override void SetParameter(string name, string value)
    {
        // Implementation...
    }

    /// <summary>
    /// Asynchronously disposes the method call context, handling both internal and external method calls.
    /// </summary>
    public override async ValueTask DisposeAsync()
    {
        if (_isDisposed || MethodCallInfo is null || MethodCallInfo.IsNull)
        {
            return;
        }

        try
        {
            EnsureValidVersion();

            _stopwatch.Stop();
            MethodCallInfo.Elapsed = _stopwatch.Elapsed;
            var endStatus = new MethodCallEnd(MethodCallInfo);

            if (_monitoringController.ShouldTrack(ContextVersion, reporterIds: ReporterIds))
            {
                (_classMonitor as ClassMonitor)?.LogStatus(endStatus);
            }

            // Handle external method call
            if (MethodCallInfo.IsExternalCall)
            {
                _logger.LogDebug($"Disposing external async method call: {MethodCallInfo.MethodName}");
                // Additional handling for external calls if needed
            }
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("outdated version"))
        {
            _logger.LogWarning($"Async method call context disposed with outdated version. Method: {MethodCallInfo.MethodName}, Context Version: {ContextVersion}, Current Version: {_monitoringController.GetCurrentVersion()}");
        }
        finally
        {
            await DisposeDisposablesAsync();
            _methodCallInfoPool.Return(MethodCallInfo);
            _isDisposed = true;
        }
    }

    public override void Dispose()
    {
        DisposeAsync().AsTask().Wait();
    }

    private async Task DisposeDisposablesAsync()
    {
        foreach (var disposable in _disposables ?? Array.Empty<IAsyncDisposable>())
        {
            try
            {
                await disposable.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing async disposable");
            }
        }
    }
}

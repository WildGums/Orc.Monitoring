namespace Orc.Monitoring;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MethodLifeCycleItems;


public sealed class MethodCallContext : MethodCallContextBase
{
    public MethodCallContext(
        IClassMonitor? classMonitor,
        MethodCallInfo methodCallInfo,
        List<IAsyncDisposable> disposables,
        IEnumerable<string> reporterIds,
        IMonitoringLoggerFactory loggerFactory,
        IMonitoringController monitoringController,
        MethodCallInfoPool methodCallInfoPool)
        : base(classMonitor, methodCallInfo, disposables, reporterIds, loggerFactory.CreateLogger<MethodCallContext>(), monitoringController, methodCallInfoPool)
    {
    }

    public override void LogException(Exception exception)
    {
        // Implementation...
    }

    public override void Log(string category, object data)
    {
        // Implementation...
    }

    public override void SetParameter(string name, string value)
    {
        // Implementation...
    }

    public override void Dispose()
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
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("outdated version"))
        {
            _logger.LogWarning($"Method call context disposed with outdated version. Method: {MethodCallInfo.MethodName}, Context Version: {ContextVersion}, Current Version: {_monitoringController.GetCurrentVersion()}");
        }
        finally
        {
            DisposeDisposables();
            _methodCallInfoPool.Return(MethodCallInfo);
            _isDisposed = true;
        }
    }

    public override ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    private void DisposeDisposables()
    {
        foreach (var disposable in _disposables ?? Array.Empty<IAsyncDisposable>())
        {
            try
            {
                disposable.DisposeAsync().AsTask().Wait();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing async disposable");
            }
        }
    }
}

namespace Orc.Monitoring;

using System;
using System.Collections.Generic;
using System.Linq;
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

            if (_monitoringController.ShouldTrack(ContextVersion, reporterIds:ReporterIds))
            {
                (_classMonitor as ClassMonitor)?.LogStatus(endStatus);
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
        foreach (var disposable in _disposables ?? [])
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

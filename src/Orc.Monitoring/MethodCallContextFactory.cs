namespace Orc.Monitoring;

using System;
using System.Collections.Generic;

public class MethodCallContextFactory(IMonitoringController monitoringController, IMonitoringLoggerFactory loggerFactory, MethodCallInfoPool methodCallInfoPool) : IMethodCallContextFactory
{
#pragma warning disable IDISP006
    private MethodCallContext? _dummy;
#pragma warning restore IDISP006
    private AsyncMethodCallContext? _asyncDummy;

    internal static MethodCallContextFactory Instance { get; } = new(MonitoringController.Instance, MonitoringLoggerFactory.Instance, MethodCallInfoPool.Instance);

    public MethodCallContext GetDummyMethodCallContext()
    {
        return _dummy ??= new MethodCallContext(loggerFactory, monitoringController, methodCallInfoPool);
    }

    public MethodCallContext CreateMethodCallContext(IClassMonitor? classMonitor, MethodCallInfo methodCallInfo, List<IAsyncDisposable> disposables, IEnumerable<string> reporterIds)
    {
        return new MethodCallContext(classMonitor, methodCallInfo, disposables, reporterIds, loggerFactory, monitoringController, methodCallInfoPool);
    }

    public AsyncMethodCallContext GetDummyAsyncMethodCallContext()
    {
        return _asyncDummy ??= new AsyncMethodCallContext(loggerFactory, monitoringController, methodCallInfoPool);
    }

    public AsyncMethodCallContext CreateAsyncMethodCallContext(IClassMonitor? classMonitor, MethodCallInfo methodCallInfo, List<IAsyncDisposable> disposables, IEnumerable<string> reporterIds)
    {
        return new AsyncMethodCallContext(classMonitor, methodCallInfo, disposables, reporterIds, loggerFactory, monitoringController, methodCallInfoPool);
    }
}

namespace Orc.Monitoring;

using System;
using System.Collections.Generic;

public class MethodCallContextFactory : IMethodCallContextFactory
{
    private readonly IMonitoringController _monitoringController;
    private readonly IMonitoringLoggerFactory _loggerFactory;
    private readonly MethodCallInfoPool _methodCallInfoPool;

#pragma warning disable IDISP006
    private MethodCallContext? _dummy;
#pragma warning restore IDISP006
    private AsyncMethodCallContext? _asyncDummy;

    public MethodCallContextFactory(IMonitoringController monitoringController, IMonitoringLoggerFactory loggerFactory, MethodCallInfoPool methodCallInfoPool)
    {
        _monitoringController = monitoringController;
        _loggerFactory = loggerFactory;
        _methodCallInfoPool = methodCallInfoPool;
    }

    internal static MethodCallContextFactory Instance { get; } = new MethodCallContextFactory(MonitoringController.Instance, MonitoringLoggerFactory.Instance, MethodCallInfoPool.Instance);

    public MethodCallContext GetDummyMethodCallContext()
    {
        return _dummy ??= new MethodCallContext(_loggerFactory, _monitoringController, _methodCallInfoPool);
    }

    public MethodCallContext CreateMethodCallContext(IClassMonitor? classMonitor, MethodCallInfo methodCallInfo, List<IAsyncDisposable> disposables, IEnumerable<string> reporterIds)
    {
        return new MethodCallContext(classMonitor, methodCallInfo, disposables, reporterIds, _loggerFactory, _monitoringController, _methodCallInfoPool);
    }

    public AsyncMethodCallContext GetDummyAsyncMethodCallContext()
    {
        return _asyncDummy ??= new AsyncMethodCallContext(_loggerFactory, _monitoringController, _methodCallInfoPool);
    }

    public AsyncMethodCallContext CreateAsyncMethodCallContext(IClassMonitor? classMonitor, MethodCallInfo methodCallInfo, List<IAsyncDisposable> disposables, IEnumerable<string> reporterIds)
    {
        return new AsyncMethodCallContext(classMonitor, methodCallInfo, disposables, reporterIds, _loggerFactory, _monitoringController, _methodCallInfoPool);
    }
}

namespace Orc.Monitoring.Core.Monitors;

using System;
using Abstractions;
using CallStacks;
using Configuration;
using Controllers;
using Logging;
using MethodCallContexts;
using Pooling;

public class ClassMonitorFactory : IClassMonitorFactory
{
    private readonly IMonitoringController _monitoringController;
    private readonly IMonitoringLoggerFactory _loggerFactory;
    private readonly IMethodCallContextFactory _methodCallContextFactory;
    private readonly MethodCallInfoPool _methodCallInfoPool;

    public ClassMonitorFactory(IMonitoringController monitoringController, IMonitoringLoggerFactory loggerFactory,
        IMethodCallContextFactory methodCallContextFactory, MethodCallInfoPool methodCallInfoPool)
    {
        ArgumentNullException.ThrowIfNull(monitoringController);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(methodCallContextFactory);
        ArgumentNullException.ThrowIfNull(methodCallInfoPool);

        _monitoringController = monitoringController;
        _loggerFactory = loggerFactory;
        _methodCallContextFactory = methodCallContextFactory;
        _methodCallInfoPool = methodCallInfoPool;
    }

    internal static IClassMonitorFactory Instance { get; } = new ClassMonitorFactory(MonitoringController.Instance, MonitoringLoggerFactory.Instance,
        MethodCallContextFactory.Instance, MethodCallInfoPool.Instance);

    public IClassMonitor CreateClassMonitor(Type type, CallStack callStack, MonitoringConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(callStack);
        ArgumentNullException.ThrowIfNull(configuration);

        return new ClassMonitor(_monitoringController, type, callStack, configuration, _loggerFactory, _methodCallContextFactory, _methodCallInfoPool);
    }

    public IClassMonitor CreateNullClassMonitor()
    {
        return new NullClassMonitor(_loggerFactory, new MethodCallContextFactory(_monitoringController, _loggerFactory, new MethodCallInfoPool(_monitoringController, _loggerFactory)));
    }
}

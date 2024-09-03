namespace Orc.Monitoring;

public class CallStackFactory : ICallStackFactory
{
    private readonly IMonitoringController _monitoringController;
    private readonly IMonitoringLoggerFactory _loggerFactory;
    private readonly MethodCallInfoPool _methodCallInfoPool;

    public CallStackFactory(IMonitoringController monitoringController, IMonitoringLoggerFactory loggerFactory, MethodCallInfoPool methodCallInfoPool)
    {
        _monitoringController = monitoringController;
        _loggerFactory = loggerFactory;
        _methodCallInfoPool = methodCallInfoPool;
    }

    public CallStack CreateCallStack(MonitoringConfiguration configuration)
    {
        return new CallStack(_monitoringController, configuration, _methodCallInfoPool, _loggerFactory);
    }

    internal static CallStackFactory Instance { get; } = new CallStackFactory(
        MonitoringController.Instance,
        MonitoringLoggerFactory.Instance,
        MethodCallInfoPool.Instance);
}

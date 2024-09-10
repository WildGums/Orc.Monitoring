namespace Orc.Monitoring;

public class CallStackFactory(IMonitoringController monitoringController, IMonitoringLoggerFactory loggerFactory, MethodCallInfoPool methodCallInfoPool) : ICallStackFactory
{
    public CallStack CreateCallStack(MonitoringConfiguration configuration)
    {
        return new CallStack(monitoringController, configuration, methodCallInfoPool, loggerFactory);
    }

    internal static CallStackFactory Instance { get; } = new(
        MonitoringController.Instance,
        MonitoringLoggerFactory.Instance,
        MethodCallInfoPool.Instance);
}

namespace Orc.Monitoring.Core.CallStacks;

using Abstractions;
using Configuration;
using Controllers;
using Monitoring.Utilities.Logging;
using Pooling;

public class CallStackFactory(IMonitoringController monitoringController, IMonitoringLoggerFactory loggerFactory, MethodCallInfoPool methodCallInfoPool) : ICallStackFactory
{
    public CallStack CreateCallStack()
    {
        return new CallStack(monitoringController, methodCallInfoPool, loggerFactory);
    }

    internal static CallStackFactory Instance { get; } = new(
        MonitoringController.Instance,
        MonitoringLoggerFactory.Instance,
        MethodCallInfoPool.Instance);
}

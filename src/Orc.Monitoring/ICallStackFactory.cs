namespace Orc.Monitoring;

public interface ICallStackFactory
{
    CallStack CreateCallStack(MonitoringConfiguration configuration);
}

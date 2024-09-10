namespace Orc.Monitoring;

using System;

public interface IClassMonitorFactory
{
    IClassMonitor CreateClassMonitor(Type type, CallStack callStack, MonitoringConfiguration configuration);
    IClassMonitor CreateNullClassMonitor();
}

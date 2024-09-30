namespace Orc.Monitoring.Core.Abstractions;

using System;
using CallStacks;
using Configuration;

public interface IClassMonitorFactory
{
    IClassMonitor CreateClassMonitor(Type type, CallStack callStack);
    IClassMonitor CreateNullClassMonitor();
}

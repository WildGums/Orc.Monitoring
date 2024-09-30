namespace Orc.Monitoring.Core.Abstractions;

using System;
using Configuration;

public interface IPerformanceMonitor
{
    bool IsConfigured { get; }
    IClassMonitor ForClass<T>();

    void Start();
    void Reset();

    IClassMonitor ForCurrentClass();
}

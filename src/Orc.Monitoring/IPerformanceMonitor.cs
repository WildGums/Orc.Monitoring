﻿namespace Orc.Monitoring;

using System;

public interface IPerformanceMonitor
{
    bool IsConfigured { get; }
    IClassMonitor ForClass<T>();
    void Configure(Action<ConfigurationBuilder> configAction);

    MonitoringConfiguration? GetCurrentConfiguration();

    void Reset();

    IClassMonitor ForCurrentClass();
}

namespace Orc.Monitoring.Core.PerformanceMonitoring;

using System;
using Abstractions;
using CallStacks;
using Configuration;
using Controllers;
using Monitoring.Utilities.Logging;
using Monitors;

public static class Performance
{
    static Performance()
    {
        var monitoringController = MonitoringController.Instance;
        Monitor = new PerformanceMonitor(monitoringController, 
            MonitoringLoggerFactory.Instance, 
            CallStackFactory.Instance, 
            ClassMonitorFactory.Instance);

        Controller = monitoringController;
    }

    public static IPerformanceMonitor Monitor { get; }

    public static IMonitoringController Controller { get; }
}

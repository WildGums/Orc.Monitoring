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
            ClassMonitorFactory.Instance,
            () => new ConfigurationBuilder(monitoringController));

        Controller = monitoringController;
    }

    public static IPerformanceMonitor Monitor { get; }

    public static IMonitoringController Controller { get; }

    public static void Configure(Action<ConfigurationBuilder> configAction)
    {
        Monitor.Configure(configAction);
    }
}

namespace Orc.Monitoring;

using System;

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

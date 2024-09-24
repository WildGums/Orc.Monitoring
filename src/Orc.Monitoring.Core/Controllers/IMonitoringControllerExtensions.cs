namespace Orc.Monitoring.Core.Controllers;

using Models;
using System.Collections.Generic;
using System;
using System.Linq;
using Abstractions;
using Microsoft.Extensions.Logging;
using Utilities.Logging;
using static Orc.Monitoring.Core.Controllers.MonitoringController;

public static class IMonitoringControllerExtensions
{
    private static readonly ILogger Logger = MonitoringLoggerFactory.Instance.CreateLogger(typeof(IMonitoringControllerExtensions));

    // Filter Extensions
    public static void EnableFilter<T>(this IMonitoringController controller) where T : IMethodFilter
    {
        controller.SetComponentState(MonitoringComponentType.Filter, typeof(T), true);
    }

    public static void DisableFilter<T>(this IMonitoringController controller) where T : IMethodFilter
    {
        controller.SetComponentState(MonitoringComponentType.Filter, typeof(T), false);
    }

    public static bool IsFilterEnabled<T>(this IMonitoringController controller) where T : IMethodFilter
    {
        return controller.GetComponentState(MonitoringComponentType.Filter, typeof(T));
    }

    // Reporter Extensions
    public static void EnableReporter<T>(this IMonitoringController controller) where T : IMethodCallReporter
    {
        controller.SetComponentState(MonitoringComponentType.Reporter, typeof(T), true);
    }

    public static void DisableReporter<T>(this IMonitoringController controller) where T : IMethodCallReporter
    {
        controller.SetComponentState(MonitoringComponentType.Reporter, typeof(T), false);
    }

    public static bool IsReporterEnabled<T>(this IMonitoringController controller) where T : IMethodCallReporter
    {
        return controller.GetComponentState(MonitoringComponentType.Reporter, typeof(T));
    }

    // Output Type Extensions
    public static void EnableOutputType<T>(this IMonitoringController controller) where T : IReportOutput
    {
        controller.SetComponentState(MonitoringComponentType.OutputType, typeof(T), true);
    }

    public static void DisableOutputType<T>(this IMonitoringController controller) where T : IReportOutput
    {
        controller.SetComponentState(MonitoringComponentType.OutputType, typeof(T), false);
    }

    public static bool IsOutputTypeEnabled<T>(this IMonitoringController controller) where T : IReportOutput
    {
        return controller.GetComponentState(MonitoringComponentType.OutputType, typeof(T));
    }

    // Methods for Type parameters
    public static void EnableFilter(this IMonitoringController controller, Type filterType)
    {
        controller.SetComponentState(MonitoringComponentType.Filter, filterType, true);
    }

    public static void DisableFilter(this IMonitoringController controller, Type filterType)
    {
        controller.SetComponentState(MonitoringComponentType.Filter, filterType, false);
    }

    public static bool IsFilterEnabled(this IMonitoringController controller, Type filterType)
    {
        return controller.GetComponentState(MonitoringComponentType.Filter, filterType);
    }

    public static void EnableReporter(this IMonitoringController controller, Type reporterType)
    {
        controller.SetComponentState(MonitoringComponentType.Reporter, reporterType, true);
    }

    public static void DisableReporter(this IMonitoringController controller, Type reporterType)
    {
        controller.SetComponentState(MonitoringComponentType.Reporter, reporterType, false);
    }

    public static bool IsReporterEnabled(this IMonitoringController controller, Type reporterType)
    {
        return controller.GetComponentState(MonitoringComponentType.Reporter, reporterType);
    }

    public static void EnableOutputType(this IMonitoringController controller, Type outputType)
    {
        controller.SetComponentState(MonitoringComponentType.OutputType, outputType, true);
    }

    public static void DisableOutputType(this IMonitoringController controller, Type outputType)
    {
        controller.SetComponentState(MonitoringComponentType.OutputType, outputType, false);
    }

    public static bool IsOutputTypeEnabled(this IMonitoringController controller, Type outputType)
    {
        return controller.GetComponentState(MonitoringComponentType.OutputType, outputType);
    }

    // ShouldTrack Method
    public static bool ShouldTrack(this IMonitoringController monitoringController, MonitoringVersion version, Type? reporterType = null, Type? filterType = null, IEnumerable<string>? reporterIds = null)
    {
        var shouldTrack = monitoringController.IsEnabled && version == monitoringController.GetCurrentVersion();
        Logger.LogDebug($"ShouldTrack called. IsEnabled: {monitoringController.IsEnabled}, VersionMatch: {version == monitoringController.GetCurrentVersion()}, Result: {shouldTrack}");

        if (!shouldTrack) return false;

        if (reporterType is not null)
        {
            shouldTrack = monitoringController.IsReporterEnabled(reporterType);
            Logger.LogDebug($"Reporter check. Type: {reporterType.Name}, Enabled: {shouldTrack}");
        }

        if (shouldTrack && filterType is not null)
        {
            if (reporterIds is not null)
            {
                shouldTrack = reporterIds.Any(id => monitoringController.IsFilterEnabledForReporter(id, filterType));
                Logger.LogDebug($"Filter check for reporters. FilterType: {filterType.Name}, Result: {shouldTrack}");
            }
            else if (reporterType is not null)
            {
                shouldTrack = monitoringController.IsFilterEnabledForReporterType(reporterType, filterType);
                Logger.LogDebug($"Filter check for reporter type. ReporterType: {reporterType.Name}, FilterType: {filterType.Name}, Result: {shouldTrack}");
            }
            else
            {
                shouldTrack = monitoringController.IsFilterEnabled(filterType);
                Logger.LogDebug($"General filter check. FilterType: {filterType.Name}, Result: {shouldTrack}");
            }
        }

        return shouldTrack;
    }

    // Reporter-Specific Filter Extensions
    public static void EnableFilterForReporterType(this IMonitoringController controller, Type reporterType, Type filterType)
    {
        if (!typeof(IMethodCallReporter).IsAssignableFrom(reporterType))
        {
            throw new ArgumentException("ReporterType must implement IMethodCallReporter", nameof(reporterType));
        }
        if (!typeof(IMethodFilter).IsAssignableFrom(filterType))
        {
            throw new ArgumentException("FilterType must implement IMethodFilter", nameof(filterType));
        }

        controller.SetFilterStateForReporterType(reporterType, filterType, true);
    }

    public static void DisableFilterForReporterType(this IMonitoringController controller, Type reporterType, Type filterType)
    {
        if (!typeof(IMethodCallReporter).IsAssignableFrom(reporterType))
        {
            throw new ArgumentException("ReporterType must implement IMethodCallReporter", nameof(reporterType));
        }
        if (!typeof(IMethodFilter).IsAssignableFrom(filterType))
        {
            throw new ArgumentException("FilterType must implement IMethodFilter", nameof(filterType));
        }

        controller.SetFilterStateForReporterType(reporterType, filterType, false);
    }

    public static bool IsFilterEnabledForReporterType(this IMonitoringController controller, Type reporterType, Type filterType)
    {
        return controller.IsFilterEnabledForReporterType(reporterType, filterType);
    }

}

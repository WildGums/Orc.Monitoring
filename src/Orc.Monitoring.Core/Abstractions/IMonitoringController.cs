namespace Orc.Monitoring.Core.Abstractions;

using System;
using System.Collections.Generic;
using Configuration;
using Controllers;
using Models;
using Utilities;

public interface IMonitoringController
{
    bool IsEnabled { get; }

    event EventHandler<VersionChangedEventArgs>? VersionChanged;

    bool ShouldTrack(MonitoringVersion version, Type? reporterType = null, Type? filterType = null, IEnumerable<string>? reporterIds = null);
    MonitoringVersion GetCurrentVersion();
    bool IsReporterEnabled<T>() where T : IMethodCallReporter;
    bool IsReporterEnabled(Type reporterType);
    bool IsFilterEnabled<T>() where T : IMethodFilter;
    bool IsFilterEnabled(Type filterType);
    void Enable();
    void Disable();
    IDisposable BeginOperation(out MonitoringVersion operationVersion);

    void EnableFilter<T>() where T : IMethodFilter;
    void EnableFilter(Type filterType);
    void EnableFilterForReporterType(Type reporterType, Type filterType);
    void DisableFilter<T>() where T : IMethodFilter;
    void DisableFilter(Type filterType);

    void EnableReporter<T>() where T : IMethodCallReporter;
    void EnableReporter(Type reporterType);
    void DisableReporter<T>() where T : IMethodCallReporter;
    void DisableReporter(Type reporterType);

    void EnableOutputType<T>() where T : IReportOutput;
    void EnableOutputType(Type outputType);

    void DisableOutputType<T>() where T : IReportOutput;
    void DisableOutputType(Type outputType);

    void RegisterContext(VersionedMonitoringContext context);

    bool IsOutputTypeEnabled<T>() where T : IReportOutput;
    bool IsOutputTypeEnabled(Type outputType);

    bool IsFilterEnabledForReporterType(Type reporterType, Type filterType);

    bool IsFilterEnabledForReporter(string reporterId, Type filterType);

    MonitoringConfiguration Configuration { get; set; }

    void AddStateChangedCallback(Action<MonitoringController.MonitoringComponentType, string, bool, MonitoringVersion> callback);
}

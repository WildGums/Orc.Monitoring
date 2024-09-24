namespace Orc.Monitoring.Core.Abstractions;

using System;
using Configuration;
using Models;
using Versioning;
using static Controllers.MonitoringController;

public interface IMonitoringController
{
    bool IsEnabled { get; }

    event EventHandler<VersionChangedEventArgs>? VersionChanged;

    MonitoringVersion GetCurrentVersion();
    void Enable();
    void Disable();
    IDisposable BeginOperation(out MonitoringVersion operationVersion);

    void RegisterContext(VersionedMonitoringContext context);

    MonitoringConfiguration Configuration { get; set; }

    void AddStateChangedCallback(Action<MonitoringComponentType, string, bool, MonitoringVersion> callback);

    void SetComponentState(MonitoringComponentType componentType, Type type, bool enabled);
    bool GetComponentState(MonitoringComponentType componentType, Type type);

    void SetFilterStateForReporterType(Type reporterType, Type filterType, bool enabled);
    bool IsFilterEnabledForReporterType(Type reporterType, Type filterType);

    void SetFilterStateForReporter(string reporterId, Type filterType, bool enabled);
    bool IsFilterEnabledForReporter(string reporterId, Type filterType);
}

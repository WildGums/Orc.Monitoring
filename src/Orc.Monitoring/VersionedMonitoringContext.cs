namespace Orc.Monitoring;

using System;

public abstract class VersionedMonitoringContext
{
    private readonly IMonitoringController _monitoringController;
    protected MonitoringVersion ContextVersion { get; private set; }

    protected VersionedMonitoringContext(IMonitoringController monitoringController)
    {
        _monitoringController = monitoringController;

        ContextVersion = _monitoringController.GetCurrentVersion();
        _monitoringController.RegisterContext(this);
    }

    protected bool IsVersionValid() =>
        ContextVersion == _monitoringController.GetCurrentVersion();

    internal void UpdateVersion(MonitoringVersion newVersion)
    {
        ContextVersion = newVersion;
        OnVersionUpdated();
    }

    protected virtual void OnVersionUpdated()
    {
        // This method can be overridden in derived classes to handle version updates
    }

    protected void EnsureValidVersion()
    {
        if (!_monitoringController.IsEnabled)
        {
            // Do nothing if monitoring is disabled
            return;
        }

        if (!IsVersionValid())
        {
            throw new InvalidOperationException("The monitoring context is operating under an outdated version.");
        }
    }
}

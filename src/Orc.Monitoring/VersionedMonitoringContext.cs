namespace Orc.Monitoring;

using System;


public abstract class VersionedMonitoringContext
{
    protected MonitoringVersion ContextVersion { get; private set; }

    protected VersionedMonitoringContext()
    {
        ContextVersion = MonitoringController.GetCurrentVersion();
        MonitoringController.RegisterContext(this);
    }

    protected bool IsVersionValid() =>
        ContextVersion == MonitoringController.GetCurrentVersion();

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
        if (!IsVersionValid())
        {
            throw new InvalidOperationException("The monitoring context is operating under an outdated version.");
        }
    }
}

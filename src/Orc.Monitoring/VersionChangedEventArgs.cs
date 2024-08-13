namespace Orc.Monitoring;

using System;

public class VersionChangedEventArgs : EventArgs
{
    public MonitoringVersion OldVersion { get; }
    public MonitoringVersion NewVersion { get; }

    public VersionChangedEventArgs(MonitoringVersion oldVersion, MonitoringVersion newVersion)
    {
        OldVersion = oldVersion;
        NewVersion = newVersion;
    }
}

namespace Orc.Monitoring;

using System;

public class VersionChangedEventArgs(MonitoringVersion oldVersion, MonitoringVersion newVersion) : EventArgs
{
    public MonitoringVersion OldVersion { get; } = oldVersion;
    public MonitoringVersion NewVersion { get; } = newVersion;
}

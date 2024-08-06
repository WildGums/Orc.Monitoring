namespace Orc.Monitoring;

using System;


public readonly struct MonitoringVersion : IEquatable<MonitoringVersion>, IComparable<MonitoringVersion>
{
    public long MainVersion { get; }
    public Guid ChangeId { get; }

    public MonitoringVersion(long mainVersion, Guid changeId)
    {
        MainVersion = mainVersion;
        ChangeId = changeId;
    }

    public bool Equals(MonitoringVersion other) =>
        MainVersion == other.MainVersion && ChangeId == other.ChangeId;

    public override bool Equals(object? obj) =>
        obj is MonitoringVersion version && Equals(version);

    public override int GetHashCode() => HashCode.Combine(MainVersion, ChangeId);

    public static bool operator ==(MonitoringVersion left, MonitoringVersion right) =>
        left.Equals(right);

    public static bool operator !=(MonitoringVersion left, MonitoringVersion right) =>
        !(left == right);

    public int CompareTo(MonitoringVersion other) =>
        MainVersion.CompareTo(other.MainVersion);

    public static bool operator <(MonitoringVersion left, MonitoringVersion right) =>
        left.CompareTo(right) < 0;

    public static bool operator >(MonitoringVersion left, MonitoringVersion right) =>
        left.CompareTo(right) > 0;

    public static bool operator <=(MonitoringVersion left, MonitoringVersion right) =>
        left.CompareTo(right) <= 0;

    public static bool operator >=(MonitoringVersion left, MonitoringVersion right) =>
        left.CompareTo(right) >= 0;

    public override string ToString() => $"V{MainVersion}-{ChangeId}";
}

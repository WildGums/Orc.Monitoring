namespace Orc.Monitoring;

using System;


/// <summary>
/// Represents a version of the monitoring system state.
/// </summary>
public readonly struct MonitoringVersion : IEquatable<MonitoringVersion>, IComparable<MonitoringVersion>
{
    /// <summary>
    /// Gets the main version number.
    /// </summary>
    public long MainVersion { get; }

    /// <summary>
    /// Gets the unique identifier for this version change.
    /// </summary>
    public Guid ChangeId { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MonitoringVersion"/> struct.
    /// </summary>
    /// <param name="mainVersion">The main version number.</param>
    /// <param name="changeId">The unique identifier for this version change.</param>
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

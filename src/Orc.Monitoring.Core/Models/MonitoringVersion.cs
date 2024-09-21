namespace Orc.Monitoring.Core.Models;

using System;

public readonly struct MonitoringVersion(long timestamp, int counter, Guid changeId) : IEquatable<MonitoringVersion>, IComparable<MonitoringVersion>
{
    public long Timestamp { get; } = timestamp;
    public int Counter { get; } = counter;
    public Guid ChangeId { get; } = changeId;

    public bool Equals(MonitoringVersion other) =>
        Timestamp == other.Timestamp && Counter == other.Counter && ChangeId.Equals(other.ChangeId);

    public override bool Equals(object? obj) =>
        obj is MonitoringVersion version && Equals(version);

    public override int GetHashCode() => HashCode.Combine(Timestamp, Counter, ChangeId);

    public static bool operator ==(MonitoringVersion left, MonitoringVersion right) =>
        left.Equals(right);

    public static bool operator !=(MonitoringVersion left, MonitoringVersion right) =>
        !(left == right);

    public int CompareTo(MonitoringVersion other)
    {
        var timestampComparison = Timestamp.CompareTo(other.Timestamp);
        if (timestampComparison != 0) return timestampComparison;
        var counterComparison = Counter.CompareTo(other.Counter);
        if (counterComparison != 0) return counterComparison;
        return ChangeId.CompareTo(other.ChangeId);
    }

    public static bool operator <(MonitoringVersion left, MonitoringVersion right) =>
        left.CompareTo(right) < 0;

    public static bool operator >(MonitoringVersion left, MonitoringVersion right) =>
        left.CompareTo(right) > 0;

    public static bool operator <=(MonitoringVersion left, MonitoringVersion right) =>
        left.CompareTo(right) <= 0;

    public static bool operator >=(MonitoringVersion left, MonitoringVersion right) =>
        left.CompareTo(right) >= 0;

    public override string ToString() => $"V{Timestamp}-{Counter}-{ChangeId}";
}

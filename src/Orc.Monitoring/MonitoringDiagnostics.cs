// ReSharper disable InconsistentNaming
#pragma warning disable IDE1006
namespace Orc.Monitoring;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

public static class MonitoringDiagnostics
{
    private static readonly ConcurrentQueue<VersionChangeRecord> _versionHistory = new();
    private static readonly ILogger _logger = MonitoringLoggerFactory.Instance.CreateLogger(typeof(MonitoringDiagnostics));
    private const int MaxHistorySize = 1000; // Adjust as needed


    /// <summary>
    /// Logs a change in the monitoring version.
    /// </summary>
    /// <param name="oldVersion">The previous version.</param>
    /// <param name="newVersion">The new version.</param>
    public static void LogVersionChange(MonitoringVersion oldVersion, MonitoringVersion newVersion)
    {
        var record = new VersionChangeRecord(oldVersion, newVersion, DateTime.UtcNow);
        _versionHistory.Enqueue(record);
        _logger.LogInformation($"Monitoring version changed from {oldVersion} to {newVersion}");

        // Trim history if it exceeds the max size
        while (_versionHistory.Count > MaxHistorySize && _versionHistory.TryDequeue(out _)) { }
    }

    /// <summary>
    /// Retrieves the full history of version changes.
    /// </summary>
    /// <returns>A read-only list of version change records.</returns>
    public static IReadOnlyList<VersionChangeRecord> GetVersionHistory()
    {
        return _versionHistory.ToArray();
    }

    public static void ClearVersionHistory()
    {
        _versionHistory.Clear();
    }

    public static MonitoringVersion GetLatestVersion()
    {
        return _versionHistory.TryPeek(out var latestRecord) ? latestRecord.NewVersion : default;
    }

    public static int GetVersionChangeCount()
    {
        return _versionHistory.Count;
    }

    public static TimeSpan GetAverageVersionDuration()
    {
        var records = _versionHistory.ToArray();
        if (records.Length < 2) return TimeSpan.Zero;

        var totalDuration = records.Skip(1)
            .Zip(records, (current, previous) => current.Timestamp - previous.Timestamp)
            .Aggregate(TimeSpan.Zero, (total, duration) => total + duration);

        return totalDuration / (records.Length - 1);
    }

    /// <summary>
    /// Generates a detailed report of all version changes.
    /// </summary>
    /// <returns>A string containing the version change report.</returns>
    public static string GenerateVersionReport()
    {
        var report = new System.Text.StringBuilder();
        report.AppendLine("Monitoring Version Change Report:");
        foreach (var record in _versionHistory)
        {
            report.AppendLine($"  {record.Timestamp:yyyy-MM-dd HH:mm:ss.fff}: {record.OldVersion} -> {record.NewVersion}");
        }
        report.AppendLine($"Total Version Changes: {GetVersionChangeCount()}");
        report.AppendLine($"Average Version Duration: {GetAverageVersionDuration()}");
        return report.ToString();
    }

    public static VersionChangeRecord? FindVersionAtTime(DateTime time)
    {
        var history = _versionHistory.ToList(); // Create a snapshot of the history
        if (history.Count == 0 || time < history[0].Timestamp)
        {
            return null; // No version changes recorded yet or time is before first change
        }
        return history
            .Where(r => r.Timestamp <= time)
            .OrderByDescending(r => r.Timestamp)
            .FirstOrDefault();
    }
}

public readonly struct VersionChangeRecord
{
    public MonitoringVersion OldVersion { get; }
    public MonitoringVersion NewVersion { get; }
    public DateTime Timestamp { get; }

    public VersionChangeRecord(MonitoringVersion oldVersion, MonitoringVersion newVersion, DateTime timestamp)
    {
        OldVersion = oldVersion;
        NewVersion = newVersion;
        Timestamp = timestamp;
    }
}

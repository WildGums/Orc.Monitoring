// ReSharper disable InconsistentNaming
#pragma warning disable IDE1006
namespace Orc.Monitoring;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;


public static class MonitoringDiagnostics
{
    private static readonly ConcurrentQueue<VersionChangeRecord> _versionHistory = new();
    private static readonly ILogger _logger = MonitoringController.CreateLogger(typeof(MonitoringDiagnostics));

    public static void LogVersionChange(MonitoringVersion oldVersion, MonitoringVersion newVersion)
    {
        var record = new VersionChangeRecord(oldVersion, newVersion, DateTime.UtcNow);
        _versionHistory.Enqueue(record);
        _logger.LogInformation($"Monitoring version changed from {oldVersion} to {newVersion}");
    }

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

    public static string GenerateVersionReport()
    {
        var report = new System.Text.StringBuilder();
        report.AppendLine("Monitoring Version Change Report:");
        foreach (var record in _versionHistory)
        {
            report.AppendLine($"  {record.Timestamp:yyyy-MM-dd HH:mm:ss.fff}: {record.OldVersion} -> {record.NewVersion}");
        }
        return report.ToString();
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

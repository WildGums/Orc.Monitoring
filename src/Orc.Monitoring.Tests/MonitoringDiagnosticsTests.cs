namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using System;
using System.Linq;
using System.Threading;


[TestFixture]
public class MonitoringDiagnosticsTests
{
    [SetUp]
    public void Setup()
    {
        MonitoringDiagnostics.ClearVersionHistory();
    }

    [Test]
    public void LogVersionChange_AddsRecordToHistory()
    {
        var oldVersion = new MonitoringVersion(100, 0, Guid.NewGuid());
        var newVersion = new MonitoringVersion(101, 0, Guid.NewGuid());

        MonitoringDiagnostics.LogVersionChange(oldVersion, newVersion);

        var history = MonitoringDiagnostics.GetVersionHistory();
        Assert.That(history, Has.Count.EqualTo(1));
        Assert.That(history[0].OldVersion, Is.EqualTo(oldVersion));
        Assert.That(history[0].NewVersion, Is.EqualTo(newVersion));
    }

    [Test]
    public void GetLatestVersion_ReturnsCorrectVersion()
    {
        var version1 = new MonitoringVersion(100, 0, Guid.NewGuid());
        var version2 = new MonitoringVersion(101, 0, Guid.NewGuid());

        MonitoringDiagnostics.LogVersionChange(version1, version2);

        var latestVersion = MonitoringDiagnostics.GetLatestVersion();
        Assert.That(latestVersion, Is.EqualTo(version2));
    }

    [Test]
    public void GetVersionChangeCount_ReturnsCorrectCount()
    {
        var version1 = new MonitoringVersion(100, 0, Guid.NewGuid());
        var version2 = new MonitoringVersion(101, 0, Guid.NewGuid());
        var version3 = new MonitoringVersion(102, 0, Guid.NewGuid());

        MonitoringDiagnostics.LogVersionChange(version1, version2);
        MonitoringDiagnostics.LogVersionChange(version2, version3);

        Assert.That(MonitoringDiagnostics.GetVersionChangeCount(), Is.EqualTo(2));
    }

    [Test]
    public void GetAverageVersionDuration_CalculatesCorrectly()
    {
        var version1 = new MonitoringVersion(100, 0, Guid.NewGuid());
        var version2 = new MonitoringVersion(101, 0, Guid.NewGuid());
        var version3 = new MonitoringVersion(102, 0, Guid.NewGuid());

        MonitoringDiagnostics.LogVersionChange(version1, version2);
        Thread.Sleep(100);
        MonitoringDiagnostics.LogVersionChange(version2, version3);

        var averageDuration = MonitoringDiagnostics.GetAverageVersionDuration();
        Assert.That(averageDuration.TotalMilliseconds, Is.GreaterThan(50).And.LessThan(150));
    }

    [Test]
    public void GenerateVersionReport_ContainsCorrectInformation()
    {
        var version1 = new MonitoringVersion(100, 0, Guid.NewGuid());
        var version2 = new MonitoringVersion(101, 0, Guid.NewGuid());

        MonitoringDiagnostics.LogVersionChange(version1, version2);

        var report = MonitoringDiagnostics.GenerateVersionReport();

        Assert.That(report, Does.Contain(version1.ToString()));
        Assert.That(report, Does.Contain(version2.ToString()));
        Assert.That(report, Does.Contain("Total Version Changes: 1"));
        Assert.That(report, Does.Contain("Average Version Duration:"));
    }

    [Test]
    public void FindVersionAtTime_ReturnsCorrectVersion()
    {
        var version1 = new MonitoringVersion(100, 0, Guid.NewGuid());
        var version2 = new MonitoringVersion(101, 0, Guid.NewGuid());
        var version3 = new MonitoringVersion(102, 0, Guid.NewGuid());

        var time1 = DateTime.UtcNow;
        MonitoringDiagnostics.LogVersionChange(version1, version2);
        Thread.Sleep(100);
        var time2 = DateTime.UtcNow;
        MonitoringDiagnostics.LogVersionChange(version2, version3);
        Thread.Sleep(100);
        var time3 = DateTime.UtcNow;

        var foundVersion1 = MonitoringDiagnostics.FindVersionAtTime(time1);
        var foundVersion2 = MonitoringDiagnostics.FindVersionAtTime(time2);
        var foundVersion3 = MonitoringDiagnostics.FindVersionAtTime(time3);

        Assert.That(foundVersion1?.NewVersion, Is.EqualTo(version2));
        Assert.That(foundVersion2?.NewVersion, Is.EqualTo(version2));
        Assert.That(foundVersion3?.NewVersion, Is.EqualTo(version3));
    }

    [Test]
    public void VersionHistory_RespectsMaxSize()
    {
        var maxSize = 1000; // This should match the MaxHistorySize in MonitoringDiagnostics

        for (int i = 0; i < maxSize + 100; i++)
        {
            var oldVersion = new MonitoringVersion((long)i, 0, Guid.NewGuid());
            var newVersion = new MonitoringVersion((long)i + 1, 0, Guid.NewGuid());
            MonitoringDiagnostics.LogVersionChange(oldVersion, newVersion);
        }

        var history = MonitoringDiagnostics.GetVersionHistory();
        Assert.That(history, Has.Count.EqualTo(maxSize));
        Assert.That(history.First().NewVersion.Timestamp, Is.GreaterThan(100));
    }
}

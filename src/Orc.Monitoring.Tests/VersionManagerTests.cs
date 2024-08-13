namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using NUnit.Framework.Legacy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


[TestFixture]
public class VersionManagerTests
{
    private VersionManager _versionManager;

    [SetUp]
    public void Setup()
    {
        _versionManager = new VersionManager();
    }

    [Test]
    public void GetNextVersion_ReturnsUniqueVersions()
    {
        var version1 = _versionManager.GetNextVersion();
        var version2 = _versionManager.GetNextVersion();

        Assert.That(version2, Is.GreaterThan(version1));
        Assert.That(version1, Is.Not.EqualTo(version2));
    }

    [Test]
    public void GetNextVersion_IncreasesCounter_WhenTimestampIsTheSame()
    {
        var version1 = _versionManager.GetNextVersion();
        var version2 = _versionManager.GetNextVersion();

        Assert.That(version2.Timestamp, Is.EqualTo(version1.Timestamp));
        Assert.That(version2.Counter, Is.EqualTo(version1.Counter + 1));
    }

    [Test]
    public void GetNextVersion_ResetsCounter_WhenTimestampChanges()
    {
        var version1 = _versionManager.GetNextVersion();

        // Simulate time passing
        System.Threading.Thread.Sleep(2);

        var version2 = _versionManager.GetNextVersion();

        Assert.That(version2.Timestamp, Is.GreaterThan(version1.Timestamp));
        Assert.That(version2.Counter, Is.EqualTo(0));
    }

    [Test]
    public void GetNextVersion_ThreadSafe()
    {
        const int numThreads = 100;
        const int versionsPerThread = 1000;

        var allVersions = new List<MonitoringVersion>[numThreads];

        var tasks = Enumerable.Range(0, numThreads).Select(i =>
            Task.Run(() =>
            {
                var versions = new List<MonitoringVersion>();
                for (int j = 0; j < versionsPerThread; j++)
                {
                    versions.Add(_versionManager.GetNextVersion());
                }
                allVersions[i] = versions;
            })).ToArray();

        Task.WaitAll(tasks);

        var flattenedVersions = allVersions.SelectMany(v => v).ToList();
        var uniqueVersions = new HashSet<MonitoringVersion>(flattenedVersions);

        Assert.That(uniqueVersions.Count, Is.EqualTo(numThreads * versionsPerThread), "All versions should be unique");

        var sortedVersions = flattenedVersions.OrderBy(v => v).ToList();
        Assert.That(sortedVersions, Is.EquivalentTo(flattenedVersions), "Versions should be in ascending order");
    }

    [Test]
    public void MonitoringVersion_Comparison()
    {
        var version1 = new MonitoringVersion(100, 0, Guid.NewGuid());
        var version2 = new MonitoringVersion(100, 1, Guid.NewGuid());
        var version3 = new MonitoringVersion(101, 0, Guid.NewGuid());

        Assert.That(version1, Is.LessThan(version2));
        Assert.That(version2, Is.LessThan(version3));
        Assert.That(version1, Is.LessThan(version3));

        Assert.That(version2, Is.GreaterThan(version1));
        Assert.That(version3, Is.GreaterThan(version2));
        Assert.That(version3, Is.GreaterThan(version1));

        Assert.That(version1, Is.Not.EqualTo(version2));
        Assert.That(version2, Is.Not.EqualTo(version3));
        Assert.That(version1, Is.Not.EqualTo(version3));
    }
}

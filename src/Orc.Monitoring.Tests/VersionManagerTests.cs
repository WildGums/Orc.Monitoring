#pragma warning disable CL0002
namespace Orc.Monitoring.Tests;

using Microsoft.Extensions.Logging;
using NUnit.Framework;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TestUtilities.Logging;

[TestFixture]
public class VersionManagerTests
{
    private VersionManager _versionManager;
    private TestLogger<VersionManagerTests> _logger;

    [SetUp]
    public void Setup()
    {
        _versionManager = new VersionManager();
        _logger = new TestLogger<VersionManagerTests>();
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
    public void GetNextVersion_IncreasesCounter_WhenTimestampIsCloseOrSame()
    {
        const int maxAttempts = 10;
        const int maxAllowedTimeDifference = 5; // milliseconds

        MonitoringVersion? version1 = null;
        MonitoringVersion? version2 = null;
        int timeDifference = int.MaxValue;

        // Try multiple times to get versions close in time
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            version1 = _versionManager.GetNextVersion();
            version2 = _versionManager.GetNextVersion();
            timeDifference = (int)Math.Abs(version2.Value.Timestamp - version1.Value.Timestamp);

            _logger.LogDebug($"Attempt {attempt + 1}: Time difference = {timeDifference} ms");

            if (timeDifference <= maxAllowedTimeDifference)
            {
                break;
            }

            Thread.Sleep(1); // Short delay before next attempt
        }

        Assert.That(version1.HasValue, Is.True, "First version should have a value");
        Assert.That(version2.HasValue, Is.True, "Second version should have a value");

        _logger.LogInformation($"Final time difference: {timeDifference} ms");
        _logger.LogInformation($"Version1: Timestamp = {version1?.Timestamp}, Counter = {version1?.Counter}");
        _logger.LogInformation($"Version2: Timestamp = {version2?.Timestamp}, Counter = {version2?.Counter}");

        Assert.That(timeDifference, Is.LessThanOrEqualTo(maxAllowedTimeDifference),
            $"Timestamps should be within {maxAllowedTimeDifference} milliseconds of each other");

        if (version1.HasValue)
        {
            if (version2.Value.Timestamp == version1.Value.Timestamp)
            {
                Assert.That(version2.Value.Counter, Is.EqualTo(version1.Value.Counter + 1),
                    "Counter should increment when timestamp is the same");
            }
            else
            {
                Assert.That(version2.Value.Counter, Is.LessThanOrEqualTo(version1.Value.Counter),
                    "Counter should not increase more than timestamp difference");
            }
        }
        else
        {
            Assert.Fail("Both versions should have values");
        }
    }

    [Test]
    public void GetNextVersion_ResetsCounter_WhenTimestampChanges()
    {
        var version1 = _versionManager.GetNextVersion();

        // Simulate time passing
        Thread.Sleep(2);

        var version2 = _versionManager.GetNextVersion();

        Assert.That(version2.Timestamp, Is.GreaterThan(version1.Timestamp));
        Assert.That(version2.Counter, Is.EqualTo(0));
    }

    [Test]
    public void GetNextVersion_HandlesRapidSuccessiveCalls()
    {
        var versions = new MonitoringVersion[1000];
        for (int i = 0; i < versions.Length; i++)
        {
            versions[i] = _versionManager.GetNextVersion();
        }

        for (int i = 1; i < versions.Length; i++)
        {
            Assert.That(versions[i], Is.GreaterThan(versions[i - 1]));
        }
    }

    [Test]
    public void GetNextVersion_ThreadSafe()
    {
        const int numThreads = 100;
        const int versionsPerThread = 1000;

        var allVersions = new ConcurrentBag<MonitoringVersion>();

        var tasks = Enumerable.Range(0, numThreads).Select(_ => Task.Run(() =>
        {
            for (int j = 0; j < versionsPerThread; j++)
            {
                allVersions.Add(_versionManager.GetNextVersion());
            }
        })).ToArray();

        Task.WaitAll(tasks);

        var uniqueVersions = new HashSet<MonitoringVersion>(allVersions);

        Assert.That(uniqueVersions.Count, Is.EqualTo(numThreads * versionsPerThread), "All versions should be unique");

        var sortedVersions = allVersions.OrderBy(v => v).ToList();
        for (int i = 1; i < sortedVersions.Count; i++)
        {
            Assert.That(sortedVersions[i], Is.GreaterThan(sortedVersions[i - 1]), $"Version at index {i} should be greater than the previous version");
        }
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

    [Test]
    public void GetNextVersion_HandlesTimestampOverflow()
    {
        // Simulate a scenario where the timestamp is about to overflow
        var highTimestamp = long.MaxValue - 1;
        var fieldInfo = typeof(VersionManager).GetField("_lastTimestamp", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        fieldInfo?.SetValue(_versionManager, highTimestamp);

        var version1 = _versionManager.GetNextVersion();
        var version2 = _versionManager.GetNextVersion();

        Assert.That(version2, Is.GreaterThan(version1));
        Assert.That(version2.Timestamp, Is.GreaterThanOrEqualTo(version1.Timestamp));
    }

    [Test]
    public async Task GetNextVersion_ConcurrentWithVaryingLoad()
    {
        const int numThreads = 10;
        const int maxIterations = 1000;
        var random = new Random();

        var versions = new ConcurrentBag<MonitoringVersion>();
        var tasks = new Task[numThreads];

        for (int i = 0; i < numThreads; i++)
        {
            tasks[i] = Task.Run(async () =>
            {
                for (int j = 0; j < maxIterations; j++)
                {
                    versions.Add(_versionManager.GetNextVersion());

                    // Simulate varying load
                    if (j % 10 == 0)
                    {
                        await Task.Delay(random.Next(1, 5));
                    }
                }
            });
        }

        await Task.WhenAll(tasks);

        var sortedVersions = versions.OrderBy(v => v).ToList();
        for (int i = 1; i < sortedVersions.Count; i++)
        {
            Assert.That(sortedVersions[i], Is.GreaterThan(sortedVersions[i - 1]));
        }

        _logger.LogInformation($"Total versions generated: {versions.Count}");
        _logger.LogInformation($"Unique versions: {new HashSet<MonitoringVersion>(versions).Count}");
    }

    [Test]
    public async Task GetNextVersion_UnderHighContention()
    {
        const int numThreads = 50;
        const int iterationsPerThread = 1000;
        var contendedVersionManager = new VersionManager();
        var versions = new ConcurrentBag<MonitoringVersion>();
#pragma warning disable IDISP001
        var barrier = new Barrier(numThreads);
#pragma warning restore IDISP001

        var tasks = Enumerable.Range(0, numThreads).Select(_ => Task.Run(() =>
        {
            barrier.SignalAndWait(); // Ensure all threads start at the same time

            for (int i = 0; i < iterationsPerThread; i++)
            {
                versions.Add(contendedVersionManager.GetNextVersion());
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        var uniqueVersions = new HashSet<MonitoringVersion>(versions);
        Assert.That(uniqueVersions.Count, Is.EqualTo(numThreads * iterationsPerThread), "All versions should be unique even under high contention");

        var sortedVersions = versions.OrderBy(v => v).ToList();
        for (int i = 1; i < sortedVersions.Count; i++)
        {
            Assert.That(sortedVersions[i], Is.GreaterThan(sortedVersions[i - 1]), $"Version at index {i} should be greater than the previous version");
        }
    }

    [Test]
    public void GetNextVersion_ConsistencyAcrossLongPeriods()
    {
        const int iterations = 1000000;
        var versions = new List<MonitoringVersion>(iterations);

        for (int i = 0; i < iterations; i++)
        {
            versions.Add(_versionManager.GetNextVersion());

            if (i % 100000 == 0)
            {
                Thread.Sleep(1); // Simulate passage of time
            }
        }

        for (int i = 1; i < versions.Count; i++)
        {
            Assert.That(versions[i], Is.GreaterThan(versions[i - 1]));
        }

        _logger.LogInformation($"First version: {versions.First()}");
        _logger.LogInformation($"Last version: {versions.Last()}");
        _logger.LogInformation($"Total time span: {versions.Last().Timestamp - versions.First().Timestamp} ms");
    }
}

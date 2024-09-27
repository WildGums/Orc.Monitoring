namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Filters;
using System.Collections.Generic;
using System.Threading;
using Core.Abstractions;
using Core.Controllers;
using Core.Models;
using TestUtilities.Logging;
using TestUtilities.Mocks;

#pragma warning disable CL0002


[TestFixture]
public class MonitoringControllerConcurrencyTests
{
    private TestLogger<MonitoringControllerConcurrencyTests> _logger;
    private TestLoggerFactory<MonitoringControllerConcurrencyTests> _loggerFactory;
    private IMonitoringController _monitoringController;

    [SetUp]
    public void Setup()
    {
        _logger = new TestLogger<MonitoringControllerConcurrencyTests>();
        _loggerFactory = new TestLoggerFactory<MonitoringControllerConcurrencyTests>(_logger);
        //_loggerFactory.EnableLoggingFor<MonitoringController>();
        _loggerFactory.EnableLoggingFor<MonitoringControllerConcurrencyTests>();
        _loggerFactory.EnableLoggingFor<MonitoringVersion>();
        _loggerFactory.EnableLoggingFor<VersionManager>();
        _monitoringController = new MonitoringController(_loggerFactory);
    }

    [Test]
    public async Task ConcurrentEnableDisable_ShouldMaintainConsistentState()
    {
        // Arrange
        var tasks = new List<Task>();
        var enableCount = 0;
        var disableCount = 0;
        var iterations = 1000; // Number of iterations for enabling/disabling

        // Act
        for (int i = 0; i < iterations; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                if (i % 2 == 0)
                {
                    _monitoringController.Enable();
                    Interlocked.Increment(ref enableCount);
                }
                else
                {
                    _monitoringController.Disable();
                    Interlocked.Increment(ref disableCount);
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        var isEnabled = _monitoringController.IsEnabled;
        var expectedState = enableCount > disableCount;

        Assert.That(isEnabled, Is.EqualTo(expectedState), "The final state of the monitoring controller is inconsistent.");
        _logger.LogInformation($"Enable count: {enableCount}, Disable count: {disableCount}, Final state: {isEnabled}");
    }

    [Test]
    public async Task ConcurrentReporterEnableDisable_ShouldBeThreadSafe()
    {
        const int iterations = 1000;
        var tasks = new Task[iterations];
        var reporterType = typeof(MockReporter);

        for (int i = 0; i < iterations; i++)
        {
            var i1 = i;
            tasks[i] = Task.Run(() =>
            {
                if (i1 % 2 == 0)
                    _monitoringController.EnableReporter(reporterType);
                else
                    _monitoringController.DisableReporter(reporterType);
            });
        }

        await Task.WhenAll(tasks);

        // The final state could be either enabled or disabled, but it should be consistent
        var finalState = _monitoringController.IsReporterEnabled(reporterType);
        _logger.LogInformation($"Final reporter state: {finalState}");
    }

    [Test]
    public async Task ConcurrentFilterEnableDisable_ShouldBeThreadSafe()
    {
        const int iterations = 1000;
        var tasks = new Task[iterations];
        var filterType = typeof(WorkflowItemFilter);

        for (int i = 0; i < iterations; i++)
        {
            var i1 = i;
            tasks[i] = Task.Run(() =>
            {
                if (i1 % 2 == 0)
                    _monitoringController.EnableFilter(filterType);
                else
                    _monitoringController.DisableFilter(filterType);
            });
        }

        await Task.WhenAll(tasks);

        // The final state could be either enabled or disabled, but it should be consistent
        var finalState = _monitoringController.IsFilterEnabled(filterType);
        _logger.LogInformation($"Final filter state: {finalState}");
    }

    [Test]
    public async Task ConcurrentVersionChanges_ShouldBeConsistent()
    {
        const int iterations = 1000;
        var tasks = new Task[iterations];
        var versions = new ConcurrentBag<MonitoringVersion>();
        var operations = new ConcurrentBag<string>();

        for (int i = 0; i < iterations; i++)
        {
            var i1 = i;
            tasks[i] = Task.Run(() =>
            {
                if (i1 % 3 == 0)
                {
                    _monitoringController.EnableReporter(typeof(MockReporter));
                    operations.Add("EnableReporter");
                }
                else if (i1 % 3 == 1)
                {
                    _monitoringController.DisableReporter(typeof(MockReporter));
                    operations.Add("DisableReporter");
                }
                else
                {
                    _monitoringController.EnableFilter(typeof(WorkflowItemFilter));
                    operations.Add("EnableFilter");
                }
                versions.Add(_monitoringController.GetCurrentVersion());
            });
        }

        await Task.WhenAll(tasks);

        var orderedVersions = versions.OrderBy(v => v).ToList();
        for (int i = 1; i < versions.Count; i++)
        {
            Assert.That(orderedVersions[i], Is.GreaterThanOrEqualTo(orderedVersions[i - 1]),
                $"Version at index {i} is not greater than or equal to the previous version");
        }

        Assert.That(versions.Count, Is.EqualTo(iterations),
            "Number of versions should match the number of operations");

        var uniqueVersions = new HashSet<MonitoringVersion>(versions);
        Assert.That(uniqueVersions.Count, Is.LessThanOrEqualTo(iterations),
            "Number of unique versions should be less than or equal to the number of operations");

        _logger.LogInformation($"Total operations: {operations.Count}");
        _logger.LogInformation($"EnableReporter: {operations.Count(o => o == "EnableReporter")}");
        _logger.LogInformation($"DisableReporter: {operations.Count(o => o == "DisableReporter")}");
        _logger.LogInformation($"EnableFilter: {operations.Count(o => o == "EnableFilter")}");
        _logger.LogInformation($"Total versions: {versions.Count}");
        _logger.LogInformation($"Unique versions: {uniqueVersions.Count}");
    }

    [Test]
    public async Task ConcurrentOperationsWithBeginOperation_ShouldHandleVersionChangesCorrectly()
    {
        const int iterations = 100;
        var tasks = new Task[iterations];
        var results = new ConcurrentBag<bool>();

        for (int i = 0; i < iterations; i++)
        {
            tasks[i] = Task.Run(async () =>
            {
                using (_monitoringController.BeginOperation(out var operationVersion))
                {
                    await Task.Delay(10); // Simulate some work
                    _monitoringController.EnableReporter(typeof(MockReporter));
                    await Task.Delay(10); // Simulate more work
                    results.Add(_monitoringController.ShouldTrack(operationVersion, typeof(MockReporter)));
                }
            });
        }

        await Task.WhenAll(tasks);

        Assert.That(results, Is.All.False, "All operations should return false for ShouldTrack after version change");
    }

    [Test]
    public void RapidConsecutiveVersionChanges_ShouldGenerateUniqueVersions()
    {
        const int iterations = 10000;
        var allVersions = new ConcurrentBag<MonitoringVersion>();
        var uniqueVersions = new ConcurrentDictionary<MonitoringVersion, object>();

        var initialVersion = _monitoringController.GetCurrentVersion();
        allVersions.Add(initialVersion);
        Assert.That(uniqueVersions.TryAdd(initialVersion, null), Is.True, $"Initial version {initialVersion} timestamped {initialVersion.Timestamp} should be unique");

        var lockObject = new object();

        Parallel.For(0, iterations, _ =>
        {
            MonitoringVersion currentVersion;

            lock (lockObject)
            {
                _monitoringController.EnableReporter(typeof(MockReporter));
                currentVersion = _monitoringController.GetCurrentVersion();
            }

            allVersions.Add(currentVersion);
            uniqueVersions.TryAdd(currentVersion, null);
            _logger.LogInformation($"New version: {currentVersion}");

            lock (lockObject)
            {
                _monitoringController.DisableReporter(typeof(MockReporter));
                currentVersion = _monitoringController.GetCurrentVersion();
            }

            allVersions.Add(currentVersion);
            uniqueVersions.TryAdd(currentVersion, null);
            _logger.LogInformation($"New version: {currentVersion}");
        });

        var uniqueCount = uniqueVersions.Count;
        var totalCount = allVersions.Count;

        _logger.LogInformation($"Total versions: {totalCount}");
        _logger.LogInformation($"Unique versions: {uniqueCount}");

        // Check that all versions are unique
        Assert.That(uniqueCount, Is.EqualTo(totalCount), "All versions should be unique");

        // Check for any duplicate versions
        var duplicates = allVersions.GroupBy(v => v)
            .Where(g => g.Count() > 1)
            .ToList();

        Assert.That(duplicates, Is.Empty, $"Found {duplicates.Count} duplicate versions");

        if (duplicates.Any())
        {
            foreach (var duplicate in duplicates.Take(5))
            {
                _logger.LogInformation($"Duplicate version: {duplicate.Key}, Count: {duplicate.Count()}");
            }
        }
    }

    [Test]
    public void RapidConsecutiveVersionChanges_ShouldMaintainSequentialConsistency()
    {
        const int iterations = 10000;
        var allVersions = new ConcurrentBag<MonitoringVersion>();

        for (int i = 0; i < iterations; i++)
        {
            _monitoringController.EnableReporter(typeof(MockReporter));
            var currentVersion = _monitoringController.GetCurrentVersion();

            allVersions.Add(currentVersion);
            _logger.LogInformation($"New version: {currentVersion}");

            _monitoringController.DisableReporter(typeof(MockReporter));
            currentVersion = _monitoringController.GetCurrentVersion();

            allVersions.Add(currentVersion);
            _logger.LogInformation($"New version: {currentVersion}");
        }

        var totalCount = allVersions.Count;

        // Check that timestamps are mostly increasing
        var timestampIncreases = allVersions.Zip(allVersions.Skip(1), (a, b) => b.Timestamp <= a.Timestamp).Count(x => x);
        Assert.That((double)timestampIncreases / (totalCount - 1), Is.GreaterThanOrEqualTo(0.99), "At least 99% of consecutive timestamps should be increasing or equal");

        // Check that sequence numbers are used correctly
        var sequenceUsage = allVersions.GroupBy(v => v.Timestamp)
            .Select(g => g.Select(v => v.Counter).OrderBy(c => c).ToList())
            .ToList();

        Assert.That(sequenceUsage, Is.All.Matches<List<int>>(seq => seq.SequenceEqual(Enumerable.Range(0, seq.Count))), 
            "Sequence numbers should start from 0 and increase consecutively for each timestamp");
    }
}

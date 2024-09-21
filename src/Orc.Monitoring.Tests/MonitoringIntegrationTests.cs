namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core.Abstractions;
using Core.CallStacks;
using Core.Configuration;
using Core.Controllers;
using Core.Diagnostics;
using Core.Extensions;
using Core.MethodCallContexts;
using Core.MethodLifecycle;
using Core.Models;
using Core.Monitors;
using Core.PerformanceMonitoring;
using Core.Pooling;
using Microsoft.Extensions.Logging;
using TestUtilities.Logging;
using TestUtilities.Mocks;
using TestUtilities.TestHelpers;

[TestFixture]
public class MonitoringIntegrationTests
{
    private MockReporter _mockReporter;
    private TestLogger<MonitoringIntegrationTests> _logger;
    private TestLoggerFactory<MonitoringIntegrationTests> _loggerFactory;
    private IMonitoringController _monitoringController;
    private IPerformanceMonitor _performanceMonitor;
    private MethodCallInfoPool _methodCallInfoPool;
    private MethodCallContextFactory _methodCallContextFactory;
    private IClassMonitorFactory _classMonitorFactory;
    private ICallStackFactory _callStackFactory;

    [SetUp]
    public void Setup()
    {
        _logger = new TestLogger<MonitoringIntegrationTests>();
        _loggerFactory = new TestLoggerFactory<MonitoringIntegrationTests>(_logger);
        _loggerFactory.EnableLoggingFor<VersionManager>();
        _loggerFactory.EnableLoggingFor<MockReporter>();
        _loggerFactory.EnableLoggingFor<TestWorkflowReporter>();
        _loggerFactory.EnableLoggingFor<MonitoringController>();
        _monitoringController = new MonitoringController(_loggerFactory);
        _methodCallInfoPool = new MethodCallInfoPool(_monitoringController, _loggerFactory);
        _methodCallContextFactory = new MethodCallContextFactory(_monitoringController, _loggerFactory, _methodCallInfoPool);

        _classMonitorFactory = new ClassMonitorFactory(_monitoringController, _loggerFactory, _methodCallContextFactory, _methodCallInfoPool);
        _callStackFactory = new CallStackFactory(_monitoringController, _loggerFactory, _methodCallInfoPool);

        _performanceMonitor = new PerformanceMonitor(_monitoringController, _loggerFactory,
            _callStackFactory,
            _classMonitorFactory,
            () => new ConfigurationBuilder(_monitoringController));

        _mockReporter = new MockReporter(_loggerFactory)
        {
            Name = "MockSequenceReporter",
            FullName = "MockSequenceReporter"
        };

        _performanceMonitor.Configure(config =>
        {
            config.AddReporterType(_mockReporter.GetType());
            config.TrackAssembly(typeof(MonitoringIntegrationTests).Assembly);
        });

        _monitoringController.Enable();
        _monitoringController.EnableReporter(_mockReporter.GetType());

        // Force re-initialization of the CallStack
        _monitoringController.Configuration = new MonitoringConfiguration();
    }

    [Test]
    public void PerformanceMonitor_RespectsHierarchicalControl()
    {
        _performanceMonitor.Configure(config =>
        {
            config.AddReporterType<MockReporter>();
        });

        _monitoringController.EnableReporter(typeof(MockReporter));

        var monitor = _performanceMonitor.ForClass<MonitoringIntegrationTests>();

        using var context = monitor.Start(builder => builder.AddReporter(new MockReporter(_loggerFactory)));

        Assert.That(_monitoringController.ShouldTrack(_monitoringController.GetCurrentVersion(), typeof(MockReporter)), Is.True);

        _monitoringController.Disable();

        Assert.That(_monitoringController.ShouldTrack(_monitoringController.GetCurrentVersion(), typeof(MockReporter)), Is.False);
    }

    [Test]
    public void CallStack_RespectsHierarchicalControl()
    {
        var configuration = _performanceMonitor.GetCurrentConfiguration();
        var callStack = new CallStack(_monitoringController, configuration!, _methodCallInfoPool, _loggerFactory);
        var observer = new TestObserver();
        var classMonitor = _classMonitorFactory.CreateClassMonitor(GetType(), callStack, configuration!);

        using (callStack.Subscribe(observer))
        {
            // Test when monitoring is enabled
            using (var context = classMonitor.StartMethod(new MethodConfiguration()))
            {
                Assert.That(observer.ReceivedItems, Has.Count.EqualTo(1), "Should receive start item when monitoring is enabled");
                Assert.That(observer.ReceivedItems[0], Is.TypeOf<MethodCallStart>(), "First item should be MethodCallStart");
            }
            Assert.That(observer.ReceivedItems, Has.Count.EqualTo(3), "Should receive end item when context is disposed");
            Assert.That(observer.ReceivedItems[0], Is.TypeOf<MethodCallStart>(), "First item should be MethodCallStart");
            Assert.That(observer.ReceivedItems[1], Is.TypeOf<MethodCallEnd>(), "Second item should be MethodCallEnd");
            Assert.That(observer.ReceivedItems[2], Is.EqualTo(CallStackItem.Empty), "Third item should be Empty, because the call stack is empty");

            // Test when monitoring is disabled
            _monitoringController.Disable();
            observer.ReceivedItems.Clear();

            using (var context = classMonitor.StartMethod(new MethodConfiguration()))
            {
                Assert.That(observer.ReceivedItems, Is.Empty, "Should not receive items when monitoring is disabled");
            }
            Assert.That(observer.ReceivedItems, Is.Empty, "Should not receive items when context is disposed and monitoring is disabled");

            // Test when monitoring is re-enabled
            _monitoringController.Enable();

            using (var context = classMonitor.StartMethod(new MethodConfiguration()))
            {
                Assert.That(observer.ReceivedItems, Has.Count.EqualTo(1), "Should receive start item when monitoring is re-enabled");
                Assert.That(observer.ReceivedItems[0], Is.TypeOf<MethodCallStart>(), "First item after re-enabling should be MethodCallStart");
            }
            Assert.That(observer.ReceivedItems, Has.Count.EqualTo(3), "Should receive end item when context is disposed after re-enabling");
            Assert.That(observer.ReceivedItems[1], Is.TypeOf<MethodCallEnd>(), "Second item after re-enabling should be MethodCallEnd");
        }
    }

    [Test]
    public async Task RootMethod_SetsRootMethodBeforeStartingReporting_SyncAndAsync()
    {
        var monitor = _performanceMonitor.ForClass<MonitoringIntegrationTests>();
        if (monitor is null || _mockReporter is null)
        {
            throw new InvalidOperationException("Monitor or reporter not initialized");
        }

        _mockReporter.Reset();

        // Test synchronous method
        using (var _ = monitor.Start(builder => builder.AddReporter(_mockReporter)))
        {
            await Task.Delay(10);
        }

        Assert.That(_mockReporter.OperationSequence, Is.EqualTo(new[] { "SetRootMethod", "StartReporting" }));
        Assert.That(_mockReporter.RootMethodName, Is.EqualTo(nameof(RootMethod_SetsRootMethodBeforeStartingReporting_SyncAndAsync)));

        // Reset for async test
        _mockReporter.Reset();

        // Test asynchronous method
        await using (var _ = monitor.AsyncStart(builder => builder.AddReporter(_mockReporter)))
        {
            await Task.Delay(10);
        }

        Assert.That(_mockReporter.OperationSequence, Is.EqualTo(new[] { "SetRootMethod", "StartReporting" }));
        Assert.That(_mockReporter.RootMethodName, Is.EqualTo(nameof(RootMethod_SetsRootMethodBeforeStartingReporting_SyncAndAsync)));
    }


    [Test, Repeat(3)]
    public void VersionChanges_AreReflectedInMonitoring()
    {
        // Clear the version history before starting the test
        MonitoringDiagnostics.ClearVersionHistory();

        var initialVersion = _monitoringController.GetCurrentVersion();
        _logger.LogInformation($"Initial Version: {initialVersion}");

        _monitoringController.EnableReporter(typeof(MockReporter));
        Task.Delay(50).Wait(); // Add a small delay
        var afterFirstEnableVersion = _monitoringController.GetCurrentVersion();
        _logger.LogInformation($"After First Enable Version: {afterFirstEnableVersion}");

        Assert.That(afterFirstEnableVersion, Is.GreaterThan(initialVersion), "Version should increase after enabling first reporter");

        // Force a version change
        _monitoringController.Configuration = new MonitoringConfiguration();
        Task.Delay(50).Wait(); // Add a small delay
        var afterConfigChangeVersion = _monitoringController.GetCurrentVersion();
        _logger.LogInformation($"After Config Change Version: {afterConfigChangeVersion}");

        Assert.That(afterConfigChangeVersion, Is.GreaterThan(afterFirstEnableVersion), "Version should increase after changing configuration");

        _monitoringController.EnableReporter(typeof(TestWorkflowReporter));
        Task.Delay(50).Wait(); // Add a small delay
        var finalVersion = _monitoringController.GetCurrentVersion();
        _logger.LogInformation($"Final Version: {finalVersion}");

        Assert.That(finalVersion, Is.GreaterThan(afterConfigChangeVersion), "Version should increase after enabling second reporter");

        var versionHistory = MonitoringDiagnostics.GetVersionHistory();
        _logger.LogInformation("Version History:");
        foreach (var change in versionHistory)
        {
            _logger.LogInformation($"  {change.Timestamp}: {change.OldVersion} -> {change.NewVersion}");
        }

        Assert.That(versionHistory, Has.Count.GreaterThanOrEqualTo(3), "Should have at least 3 version changes");
        Assert.That(versionHistory.First().OldVersion, Is.EqualTo(initialVersion), "First change should start from initial version");
        Assert.That(versionHistory.Last().NewVersion, Is.EqualTo(finalVersion), "Last change should end with final version");
    }

    [Test]
    public async Task ConcurrentOperations_MaintainVersionIntegrityAsync()
    {
        const int concurrentTasks = 50;
        var tasks = new Task[concurrentTasks];
        var versions = new System.Collections.Concurrent.ConcurrentBag<MonitoringVersion>();

        for (int i = 0; i < concurrentTasks; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                _monitoringController.EnableReporter(typeof(MockReporter));
                versions.Add(_monitoringController.GetCurrentVersion());
                _monitoringController.DisableReporter(typeof(MockReporter));
                versions.Add(_monitoringController.GetCurrentVersion());
            });
        }

        await Task.WhenAll(tasks);

        var orderedVersions = versions.OrderBy(v => v).ToList();
        for (int i = 1; i < orderedVersions.Count; i++)
        {
            Assert.That(orderedVersions[i], Is.GreaterThanOrEqualTo(orderedVersions[i - 1]));
        }

        var versionHistory = MonitoringDiagnostics.GetVersionHistory();
        Assert.That(versionHistory, Has.Count.GreaterThanOrEqualTo(concurrentTasks * 2));
    }

    [Test]
    public async Task LongRunningOperation_MaintainsConsistentViewAsync()
    {
        var initialVersion = _monitoringController.GetCurrentVersion();

        var operationTask = Task.Run(async () =>
        {
            using (_monitoringController.BeginOperation(out var operationVersion))
            {
                Assert.That(operationVersion, Is.EqualTo(initialVersion));
                await Task.Delay(1000); // Simulate long-running operation
                return _monitoringController.ShouldTrack(operationVersion, typeof(MockReporter));
            }
        });

        await Task.Delay(100); // Give some time for the operation to start

        _monitoringController.EnableReporter(typeof(MockReporter));
        _monitoringController.DisableReporter(typeof(MockReporter));

        var result = await operationTask;

        Assert.That(result, Is.False);
        Assert.That(_monitoringController.ShouldTrack(_monitoringController.GetCurrentVersion(), typeof(MockReporter)), Is.False);
    }

    private class TestObserver : IObserver<ICallStackItem>
    {
        public List<ICallStackItem> ReceivedItems { get; } = [];

        public void OnCompleted() { }
        public void OnError(Exception error) { }
        public void OnNext(ICallStackItem value) => ReceivedItems.Add(value);
    }
}

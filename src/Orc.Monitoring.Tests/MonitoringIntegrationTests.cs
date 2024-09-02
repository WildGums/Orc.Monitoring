namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using Filters;
using MethodLifeCycleItems;
using Reporters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

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

    [SetUp]
    public void Setup()
    {
        _logger = new TestLogger<MonitoringIntegrationTests>();
        _loggerFactory = new TestLoggerFactory<MonitoringIntegrationTests>(_logger);
        _monitoringController = new MonitoringController(_loggerFactory, () => new EnhancedDataPostProcessor(_loggerFactory));
        _methodCallInfoPool = new MethodCallInfoPool(_monitoringController, _loggerFactory);
        _methodCallContextFactory = new MethodCallContextFactory(_monitoringController, _loggerFactory, _methodCallInfoPool);

        _classMonitorFactory = new ClassMonitorFactory(_monitoringController, _loggerFactory, _methodCallContextFactory, _methodCallInfoPool);

        _performanceMonitor = new PerformanceMonitor(_monitoringController, _loggerFactory,
            (config) => new CallStack(_monitoringController, config, _methodCallInfoPool, _loggerFactory),
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
        var callStack = new CallStack(_monitoringController, configuration, _methodCallInfoPool, _loggerFactory);
        var observer = new TestObserver();

        using (callStack.Subscribe(observer))
        {
            var config = new MethodCallContextConfig { ClassType = GetType(), CallerMethodName = nameof(CallStack_RespectsHierarchicalControl) };

            var methodCallInfo = callStack.CreateMethodCallInfo(null, GetType(), config);
            callStack.Push(methodCallInfo);
            Assert.That(observer.ReceivedItems, Is.Not.Empty, "Should receive items when monitoring is enabled");

            _monitoringController.Disable();
            observer.ReceivedItems.Clear();

            callStack.Pop(methodCallInfo);
            Assert.That(observer.ReceivedItems, Is.Empty, "Should not receive items when monitoring is disabled");

            _monitoringController.Enable();
            methodCallInfo = callStack.CreateMethodCallInfo(null, GetType(), config);
            callStack.Push(methodCallInfo);
            Assert.That(observer.ReceivedItems, Is.Not.Empty, "Should receive items when monitoring is re-enabled");
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
        using (var context = monitor.Start(builder => builder.AddReporter(_mockReporter)))
        {
            await Task.Delay(10);
        }

        Assert.That(_mockReporter.OperationSequence, Is.EqualTo(new[] { "SetRootMethod", "StartReporting" }));
        Assert.That(_mockReporter.RootMethodName, Is.EqualTo(nameof(RootMethod_SetsRootMethodBeforeStartingReporting_SyncAndAsync)));

        // Reset for async test
        _mockReporter.Reset();

        // Test asynchronous method
        await using (var context = monitor.AsyncStart(builder => builder.AddReporter(_mockReporter)))
        {
            await Task.Delay(10);
        }

        Assert.That(_mockReporter.OperationSequence, Is.EqualTo(new[] { "SetRootMethod", "StartReporting" }));
        Assert.That(_mockReporter.RootMethodName, Is.EqualTo(nameof(RootMethod_SetsRootMethodBeforeStartingReporting_SyncAndAsync)));
    }


    [Test, Retry(3)] // Retry up to 3 times
    public void VersionChanges_AreReflectedInMonitoring()
    {
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
        public List<ICallStackItem> ReceivedItems { get; } = new List<ICallStackItem>();

        public void OnCompleted() { }
        public void OnError(Exception error) { }
        public void OnNext(ICallStackItem value) => ReceivedItems.Add(value);
    }
}

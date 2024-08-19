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


[TestFixture]
public class MonitoringIntegrationTests
{
    private MockReporter _mockReporter;

    [SetUp]
    public void Setup()
    {
        MonitoringController.ResetForTesting();
        _mockReporter = new MockReporter
        {
            Name = "MockSequenceReporter",
            FullName = "MockSequenceReporter"
        };

        PerformanceMonitor.Configure(config =>
        {
            config.AddReporter(_mockReporter.GetType());
            config.TrackAssembly(typeof(MonitoringIntegrationTests).Assembly);
        });

        foreach (var method in typeof(MonitoringIntegrationTests).GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            PerformanceMonitor.AddTrackedMethod(typeof(MonitoringIntegrationTests), method);
        }

        MonitoringController.Enable();
        MonitoringController.EnableReporter(_mockReporter.GetType());

        // Force re-initialization of the CallStack
        MonitoringController.Configuration = new MonitoringConfiguration();
    }

    [Test]
    public void PerformanceMonitor_RespectsHierarchicalControl()
    {
        PerformanceMonitor.Configure(config =>
        {
            config.AddReporter<MockReporter>();
        });

        MonitoringController.EnableReporter(typeof(MockReporter));

        var monitor = PerformanceMonitor.ForClass<MonitoringIntegrationTests>();

        using var context = monitor.Start(builder => builder.AddReporter<MockReporter>());

        Assert.That(MonitoringController.ShouldTrack(MonitoringController.GetCurrentVersion(), typeof(MockReporter)), Is.True);

        MonitoringController.Disable();

        Assert.That(MonitoringController.ShouldTrack(MonitoringController.GetCurrentVersion(), typeof(MockReporter)), Is.False);
    }

    [Test]
    public void CallStack_RespectsHierarchicalControl()
    {
        var callStack = new CallStack(new MonitoringConfiguration());
        var observer = new TestObserver();

        using (callStack.Subscribe(observer))
        {
            var config = new MethodCallContextConfig { ClassType = GetType(), CallerMethodName = nameof(CallStack_RespectsHierarchicalControl) };

            var methodCallInfo = callStack.CreateMethodCallInfo(null, GetType(), config);
            callStack.Push(methodCallInfo);
            Assert.That(observer.ReceivedItems, Is.Not.Empty, "Should receive items when monitoring is enabled");

            MonitoringController.Disable();
            observer.ReceivedItems.Clear();

            callStack.Pop(methodCallInfo);
            Assert.That(observer.ReceivedItems, Is.Empty, "Should not receive items when monitoring is disabled");

            MonitoringController.Enable();
            methodCallInfo = callStack.CreateMethodCallInfo(null, GetType(), config);
            callStack.Push(methodCallInfo);
            Assert.That(observer.ReceivedItems, Is.Not.Empty, "Should receive items when monitoring is re-enabled");
        }
    }

    [Test]
    public async Task RootMethod_SetsRootMethodBeforeStartingReporting_SyncAndAsync()
    {
        var monitor = PerformanceMonitor.ForClass<MonitoringIntegrationTests>();
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
        MonitoringController.ResetForTesting(); // Ensure a clean state
        var initialVersion = MonitoringController.GetCurrentVersion();
        Console.WriteLine($"Initial Version: {initialVersion}");

        MonitoringController.EnableReporter(typeof(MockReporter));
        Task.Delay(50).Wait(); // Add a small delay
        var afterFirstEnableVersion = MonitoringController.GetCurrentVersion();
        Console.WriteLine($"After First Enable Version: {afterFirstEnableVersion}");

        Assert.That(afterFirstEnableVersion, Is.GreaterThan(initialVersion), "Version should increase after enabling first reporter");

        // Force a version change
        MonitoringController.Configuration = new MonitoringConfiguration();
        Task.Delay(50).Wait(); // Add a small delay
        var afterConfigChangeVersion = MonitoringController.GetCurrentVersion();
        Console.WriteLine($"After Config Change Version: {afterConfigChangeVersion}");

        Assert.That(afterConfigChangeVersion, Is.GreaterThan(afterFirstEnableVersion), "Version should increase after changing configuration");

        MonitoringController.EnableReporter(typeof(WorkflowReporter));
        Task.Delay(50).Wait(); // Add a small delay
        var finalVersion = MonitoringController.GetCurrentVersion();
        Console.WriteLine($"Final Version: {finalVersion}");

        Assert.That(finalVersion, Is.GreaterThan(afterConfigChangeVersion), "Version should increase after enabling second reporter");

        var versionHistory = MonitoringDiagnostics.GetVersionHistory();
        Console.WriteLine("Version History:");
        foreach (var change in versionHistory)
        {
            Console.WriteLine($"  {change.Timestamp}: {change.OldVersion} -> {change.NewVersion}");
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
                MonitoringController.EnableReporter(typeof(MockReporter));
                versions.Add(MonitoringController.GetCurrentVersion());
                MonitoringController.DisableReporter(typeof(MockReporter));
                versions.Add(MonitoringController.GetCurrentVersion());
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
        var initialVersion = MonitoringController.GetCurrentVersion();

        var operationTask = Task.Run(async () =>
        {
            using (MonitoringController.BeginOperation(out var operationVersion))
            {
                Assert.That(operationVersion, Is.EqualTo(initialVersion));
                await Task.Delay(1000); // Simulate long-running operation
                return MonitoringController.ShouldTrack(operationVersion, typeof(MockReporter));
            }
        });

        await Task.Delay(100); // Give some time for the operation to start

        MonitoringController.EnableReporter(typeof(MockReporter));
        MonitoringController.DisableReporter(typeof(MockReporter));

        var result = await operationTask;

        Assert.That(result, Is.False);
        Assert.That(MonitoringController.ShouldTrack(MonitoringController.GetCurrentVersion(), typeof(MockReporter)), Is.False);
    }

    private class TestObserver : IObserver<ICallStackItem>
    {
        public List<ICallStackItem> ReceivedItems { get; } = new List<ICallStackItem>();

        public void OnCompleted() { }
        public void OnError(Exception error) { }
        public void OnNext(ICallStackItem value) => ReceivedItems.Add(value);
    }
}

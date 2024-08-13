#pragma warning disable CL0002
namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using Filters;
using MethodLifeCycleItems;
using Reporters;
using Reporters.ReportOutputs;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;


[TestFixture]
public class MonitoringIntegrationTests
{
    private MockSequenceReporter? _sequenceReporter;

    [SetUp]
    public void Setup()
    {
        try
        {
            MonitoringController.ResetForTesting();

            _sequenceReporter = new MockSequenceReporter();

            PerformanceMonitor.Configure(config =>
            {
                config.AddReporter(_sequenceReporter.GetType());
                config.TrackAssembly(typeof(MonitoringIntegrationTests).Assembly);
            });

            // Add all public methods of the test class to tracked methods
            foreach (var method in typeof(MonitoringIntegrationTests).GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                PerformanceMonitor.AddTrackedMethod(typeof(MonitoringIntegrationTests), method);
            }

            MonitoringController.Enable();
            MonitoringController.EnableReporter(_sequenceReporter.GetType());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during test setup: {ex}");
            throw; // Rethrow the exception to fail the test
        }
    }

    [Test]
    public void PerformanceMonitor_RespectsHierarchicalControl()
    {
        PerformanceMonitor.Configure(config =>
        {
            config.AddReporter<PerformanceReporter>();
            config.AddFilter<PerformanceFilter>();
        });

        MonitoringController.EnableReporter(typeof(PerformanceReporter));

        var monitor = PerformanceMonitor.ForClass<MonitoringIntegrationTests>();

        using var context = monitor.Start(builder => builder.AddReporter<PerformanceReporter>());

        Assert.That(MonitoringController.ShouldTrack(MonitoringController.GetCurrentVersion(), typeof(PerformanceReporter)), Is.True);

        MonitoringController.Disable();

        Assert.That(MonitoringController.ShouldTrack(MonitoringController.GetCurrentVersion(), typeof(PerformanceReporter)), Is.False);
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
    public void RootMethod_SetsRootMethodBeforeStartingReporting()
    {
        var monitor = PerformanceMonitor.ForClass<MonitoringIntegrationTests>();

        if (monitor is null)
        {
            throw new InvalidOperationException("Monitor not initialized");
        }

        if (_sequenceReporter is null)
        {
            throw new InvalidOperationException("Reporter not initialized");
        }

        using (var context = monitor.Start(builder => builder.AddReporter(_sequenceReporter)))
        {
            System.Threading.Thread.Sleep(10);
        }

        Assert.That(_sequenceReporter.OperationSequence, Is.EqualTo(new[] { "SetRootMethod", "StartReporting" }));
        Assert.That(_sequenceReporter.RootMethodName, Is.EqualTo(nameof(RootMethod_SetsRootMethodBeforeStartingReporting)));
    }

    [Test]
    public async Task AsyncRootMethod_SetsRootMethodBeforeStartingReportingAsync()
    {
        var monitor = PerformanceMonitor.ForClass<MonitoringIntegrationTests>();
        if (monitor is null)
        {
            throw new InvalidOperationException("Monitor not initialized");
        }

        if (_sequenceReporter is null)
        {
            throw new InvalidOperationException("Reporter not initialized");
        }

        await using (var context = monitor.AsyncStart(builder => builder.AddReporter(_sequenceReporter)))
        {
            await Task.Delay(10);
        }

        Assert.That(_sequenceReporter.OperationSequence, Is.EqualTo(new[] { "SetRootMethod", "StartReporting" }));
        Assert.That(_sequenceReporter.RootMethodName, Is.EqualTo(nameof(AsyncRootMethod_SetsRootMethodBeforeStartingReportingAsync)));
    }

    [Test]
    public void VersionChanges_AreReflectedInMonitoring()
    {
        MonitoringController.ResetForTesting(); // Ensure a clean state
        var initialVersion = MonitoringController.GetCurrentVersion();

        MonitoringController.EnableReporter(typeof(PerformanceReporter));
        var afterEnableVersion = MonitoringController.GetCurrentVersion();

        MonitoringController.DisableReporter(typeof(PerformanceReporter));
        var finalVersion = MonitoringController.GetCurrentVersion();

        Assert.That(afterEnableVersion, Is.GreaterThan(initialVersion));
        Assert.That(finalVersion, Is.GreaterThan(afterEnableVersion));

        var versionHistory = MonitoringDiagnostics.GetVersionHistory();
        Assert.That(versionHistory, Has.Count.GreaterThanOrEqualTo(2));
        Assert.That(versionHistory.First().OldVersion, Is.EqualTo(initialVersion));
        Assert.That(versionHistory.Last().NewVersion, Is.EqualTo(finalVersion));
    }

    [Test]
    public async Task ConcurrentOperations_MaintainVersionIntegrity()
    {
        const int concurrentTasks = 50;
        var tasks = new Task[concurrentTasks];
        var versions = new ConcurrentBag<MonitoringVersion>();

        for (int i = 0; i < concurrentTasks; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                MonitoringController.EnableReporter(typeof(PerformanceReporter));
                versions.Add(MonitoringController.GetCurrentVersion());
                MonitoringController.DisableReporter(typeof(PerformanceReporter));
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
    public async Task LongRunningOperation_MaintainsConsistentView()
    {
        var initialVersion = MonitoringController.GetCurrentVersion();

        var operationTask = Task.Run(async () =>
        {
            using (MonitoringController.BeginOperation(out var operationVersion))
            {
                Assert.That(operationVersion, Is.EqualTo(initialVersion));
                await Task.Delay(1000); // Simulate long-running operation
                return MonitoringController.ShouldTrack(operationVersion, typeof(PerformanceReporter));
            }
        });

        await Task.Delay(100); // Give some time for the operation to start

        MonitoringController.EnableReporter(typeof(PerformanceReporter));
        MonitoringController.DisableReporter(typeof(PerformanceReporter));

        var result = await operationTask;

        Assert.That(result, Is.False);
        Assert.That(MonitoringController.ShouldTrack(MonitoringController.GetCurrentVersion(), typeof(PerformanceReporter)), Is.False);
    }

    private class TestObserver : IObserver<ICallStackItem>
    {
        public List<ICallStackItem> ReceivedItems { get; } = [];

        public void OnCompleted() { }
        public void OnError(Exception error) { }
        public void OnNext(ICallStackItem value) => ReceivedItems.Add(value);
    }

    private class MockSequenceReporter : IMethodCallReporter
    {
        public List<string> OperationSequence { get; } = [];
        public string? RootMethodName { get; private set; }

        public string Name => "MockSequenceReporter";
        public string FullName => "MockSequenceReporter";

        private MethodInfo? _rootMethod;
        public MethodInfo? RootMethod
        {
            get => _rootMethod;
            set
            {
                _rootMethod = value;
                RootMethodName = value?.Name;
                OperationSequence.Add("SetRootMethod");
            }
        }

        public IAsyncDisposable StartReporting(IObservable<ICallStackItem> callStack)
        {
            OperationSequence.Add("StartReporting");
            return new AsyncDisposable(async () => { });
        }

        public IOutputContainer AddOutput<TOutput>(object? parameter = null) where TOutput : IReportOutput, new()
        {
            return this;
        }
    }
}

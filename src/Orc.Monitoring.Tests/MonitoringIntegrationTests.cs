namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using Orc.Monitoring.Filters;
using Orc.Monitoring.MethodLifeCycleItems;
using Orc.Monitoring.Reporters;
using System.Collections.Generic;
using System;

[TestFixture]
public class MonitoringIntegrationTests
{
    [SetUp]
    public void Setup()
    {
#if DEBUG || TEST
        MonitoringController.ResetForTesting();
#endif
        MonitoringController.Enable();
    }

    [Test]
    public void PerformanceMonitor_RespectsHierarchicalControl()
    {
        PerformanceMonitor.Configure(config =>
        {
            config.AddReporter<PerformanceReporter>();
            config.AddFilter<PerformanceFilter>();
        });

        var monitor = PerformanceMonitor.ForClass<MonitoringIntegrationTests>();

        using (var context = monitor.Start(builder => builder.AddReporter<PerformanceReporter>()))
        {
            Assert.That(MonitoringController.ShouldTrack(MonitoringController.GetCurrentVersion(), typeof(PerformanceReporter)), Is.True);

            MonitoringController.Disable();

            Assert.That(MonitoringController.ShouldTrack(MonitoringController.GetCurrentVersion(), typeof(PerformanceReporter)), Is.False);
        }
    }

    [Test]
    public void CallStack_RespectsHierarchicalControl()
    {
        MonitoringController.Enable(); // Ensure monitoring is enabled at the start
        var callStack = new CallStack(new MonitoringConfiguration());
        var observer = new TestObserver();

        using (callStack.Subscribe(observer))
        {
            var methodInfo = typeof(MonitoringIntegrationTests).GetMethod(nameof(CallStack_RespectsHierarchicalControl));
            var config = new MethodCallContextConfig { ClassType = GetType(), CallerMethodName = nameof(CallStack_RespectsHierarchicalControl) };

            var methodCallInfo = callStack.Push(null, GetType(), config);
            Assert.That(observer.ReceivedItems, Is.Not.Empty, "Should receive items when monitoring is enabled");

            MonitoringController.Disable();
            observer.ReceivedItems.Clear();

            callStack.Pop(methodCallInfo);
            Assert.That(observer.ReceivedItems, Is.Empty, "Should not receive items when monitoring is disabled");

            MonitoringController.Enable();
            methodCallInfo = callStack.Push(null, GetType(), config);
            Assert.That(observer.ReceivedItems, Is.Not.Empty, "Should receive items when monitoring is re-enabled");
        }
    }

    private class TestObserver : IObserver<ICallStackItem>
    {
        public List<ICallStackItem> ReceivedItems { get; } = new List<ICallStackItem>();

        public void OnCompleted() { }
        public void OnError(Exception error) { }
        public void OnNext(ICallStackItem value) => ReceivedItems.Add(value);
    }
}

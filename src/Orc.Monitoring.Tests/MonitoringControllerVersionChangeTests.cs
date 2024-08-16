namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using Orc.Monitoring.Reporters;
using Orc.Monitoring.Reporters.ReportOutputs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;


[TestFixture]
public class MonitoringControllerVersionChangeTests
{
    [SetUp]
    public void Setup()
    {
        MonitoringController.ResetForTesting();
        MonitoringController.Enable();
    }

    [Test]
    public void VersionChanged_EventFired_WhenVersionChanges()
    {
        var eventFired = false;
        MonitoringVersion? oldVersion = null;
        MonitoringVersion? newVersion = null;

        MonitoringController.VersionChanged += (sender, args) =>
        {
            eventFired = true;
            oldVersion = args.OldVersion;
            newVersion = args.NewVersion;
        };

        var initialVersion = MonitoringController.GetCurrentVersion();
        MonitoringController.EnableReporter(typeof(DummyReporter));

        Assert.Multiple(() =>
        {
            Assert.That(eventFired, Is.True, "VersionChanged event should have fired");
            Assert.That(oldVersion, Is.EqualTo(initialVersion), "Old version should match the initial version");
            Assert.That(newVersion, Is.Not.EqualTo(initialVersion), "New version should be different from the initial version");
            Assert.That(newVersion, Is.EqualTo(MonitoringController.GetCurrentVersion()), "New version should match the current version");
        });
    }

    [Test]
    public void VersionChanged_MultipleSubscribers_AllNotified()
    {
        var subscriberCount = 3;
        var notifiedSubscribers = 0;

        for (int i = 0; i < subscriberCount; i++)
        {
            MonitoringController.VersionChanged += (sender, args) => notifiedSubscribers++;
        }

        MonitoringController.EnableReporter(typeof(DummyReporter));

        Assert.That(notifiedSubscribers, Is.EqualTo(subscriberCount), "All subscribers should have been notified");
    }

    [Test]
    public void VersionChanged_Unsubscribe_NoLongerNotified()
    {
        var notificationCount = 0;
        EventHandler<VersionChangedEventArgs> handler = (sender, args) => notificationCount++;

        MonitoringController.VersionChanged += handler;
        MonitoringController.EnableReporter(typeof(DummyReporter));
        Assert.That(notificationCount, Is.EqualTo(1), "Subscriber should have been notified once");

        MonitoringController.VersionChanged -= handler;
        MonitoringController.DisableReporter(typeof(DummyReporter));
        Assert.That(notificationCount, Is.EqualTo(1), "Unsubscribed handler should not have been notified");
    }

    [Test]
    public void VersionChanged_ConcurrentSubscribers_AllNotified()
    {
        const int subscriberCount = 100;
        var notifiedSubscribers = new bool[subscriberCount];

        var tasks = new List<Task>();
        for (int i = 0; i < subscriberCount; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
            {
                MonitoringController.VersionChanged += (sender, args) => notifiedSubscribers[index] = true;
            }));
        }

        Task.WaitAll(tasks.ToArray());

        MonitoringController.EnableReporter(typeof(DummyReporter));

        Assert.That(notifiedSubscribers, Is.All.True, "All concurrent subscribers should have been notified");
    }

    [Test]
    public void PropagateVersionChange_UpdatesAllActiveContexts()
    {
        var context1 = new TestVersionedMonitoringContext();
        var context2 = new TestVersionedMonitoringContext();

        MonitoringController.RegisterContext(context1);
        MonitoringController.RegisterContext(context2);

        var initialVersion = MonitoringController.GetCurrentVersion();
        MonitoringController.EnableReporter(typeof(DummyReporter));
        var newVersion = MonitoringController.GetCurrentVersion();

        Assert.Multiple(() =>
        {
            Assert.That(context1.CurrentVersion, Is.EqualTo(newVersion), "Context 1 should have been updated to the new version");
            Assert.That(context2.CurrentVersion, Is.EqualTo(newVersion), "Context 2 should have been updated to the new version");
            Assert.That(context1.CurrentVersion, Is.Not.EqualTo(initialVersion), "Context 1 should not have the initial version");
            Assert.That(context2.CurrentVersion, Is.Not.EqualTo(initialVersion), "Context 2 should not have the initial version");
        });
    }

    private class DummyReporter : IMethodCallReporter
    {
        public string Name => "DummyReporter";
        public string FullName => "DummyReporter";
        public System.Reflection.MethodInfo? RootMethod { get; set; }

        public IAsyncDisposable StartReporting(IObservable<MethodLifeCycleItems.ICallStackItem> callStack)
        {
            throw new NotImplementedException();
        }

        public IOutputContainer AddOutput<TOutput>(object? parameter = null) where TOutput : IReportOutput, new()
        {
            throw new NotImplementedException();
        }
    }

    private class TestVersionedMonitoringContext : VersionedMonitoringContext
    {
        public MonitoringVersion CurrentVersion => ContextVersion;
    }
}

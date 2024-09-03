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
    private TestLogger<MonitoringControllerVersionChangeTests> _logger;
    private TestLoggerFactory<MonitoringControllerVersionChangeTests> _loggerFactory;
    private IMonitoringController _monitoringController;
    [SetUp]
    public void Setup()
    {
        _logger = new TestLogger<MonitoringControllerVersionChangeTests>();
        _loggerFactory = new TestLoggerFactory<MonitoringControllerVersionChangeTests>(_logger);
        _monitoringController = new MonitoringController(_loggerFactory, () => new EnhancedDataPostProcessor(_loggerFactory));

        _monitoringController.Enable();
    }

    [Test]
    public void VersionChanged_EventFired_WhenVersionChanges()
    {
        var eventFired = false;
        MonitoringVersion? oldVersion = null;
        MonitoringVersion? newVersion = null;

        _monitoringController.VersionChanged += (sender, args) =>
        {
            eventFired = true;
            oldVersion = args.OldVersion;
            newVersion = args.NewVersion;
        };

        var initialVersion = _monitoringController.GetCurrentVersion();
        _monitoringController.EnableReporter(typeof(MockReporter));

        Assert.Multiple(() =>
        {
            Assert.That(eventFired, Is.True, "VersionChanged event should have fired");
            Assert.That(oldVersion, Is.EqualTo(initialVersion), "Old version should match the initial version");
            Assert.That(newVersion, Is.Not.EqualTo(initialVersion), "New version should be different from the initial version");
            Assert.That(newVersion, Is.EqualTo(_monitoringController.GetCurrentVersion()), "New version should match the current version");
        });
    }

    [Test]
    public void VersionChanged_MultipleSubscribers_AllNotified()
    {
        var subscriberCount = 3;
        var notifiedSubscribers = 0;

        for (int i = 0; i < subscriberCount; i++)
        {
            _monitoringController.VersionChanged += (sender, args) => notifiedSubscribers++;
        }

        _monitoringController.EnableReporter(typeof(MockReporter));

        Assert.That(notifiedSubscribers, Is.EqualTo(subscriberCount), "All subscribers should have been notified");
    }

    [Test]
    public void VersionChanged_Unsubscribe_NoLongerNotified()
    {
        var notificationCount = 0;
        EventHandler<VersionChangedEventArgs> handler = (sender, args) => notificationCount++;

        _monitoringController.VersionChanged += handler;
        _monitoringController.EnableReporter(typeof(MockReporter));
        Assert.That(notificationCount, Is.EqualTo(1), "Subscriber should have been notified once");

        _monitoringController.VersionChanged -= handler;
        _monitoringController.DisableReporter(typeof(MockReporter));
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
                _monitoringController.VersionChanged += (sender, args) => notifiedSubscribers[index] = true;
            }));
        }

        Task.WaitAll(tasks.ToArray());

        _monitoringController.EnableReporter(typeof(MockReporter));

        Assert.That(notifiedSubscribers, Is.All.True, "All concurrent subscribers should have been notified");
    }

    [Test]
    public void PropagateVersionChange_UpdatesAllActiveContexts()
    {
        var context1 = new TestVersionedMonitoringContext(_monitoringController);
        var context2 = new TestVersionedMonitoringContext(_monitoringController);

        _monitoringController.RegisterContext(context1);
        _monitoringController.RegisterContext(context2);

        var initialVersion = _monitoringController.GetCurrentVersion();
        _monitoringController.EnableReporter(typeof(MockReporter));
        var newVersion = _monitoringController.GetCurrentVersion();

        Assert.Multiple(() =>
        {
            Assert.That(context1.CurrentVersion, Is.EqualTo(newVersion), "Context 1 should have been updated to the new version");
            Assert.That(context2.CurrentVersion, Is.EqualTo(newVersion), "Context 2 should have been updated to the new version");
            Assert.That(context1.CurrentVersion, Is.Not.EqualTo(initialVersion), "Context 1 should not have the initial version");
            Assert.That(context2.CurrentVersion, Is.Not.EqualTo(initialVersion), "Context 2 should not have the initial version");
        });
    }

    private class TestVersionedMonitoringContext : VersionedMonitoringContext
    {
        public TestVersionedMonitoringContext(IMonitoringController monitoringController)
        : base(monitoringController)
        {
            
        }

        public MonitoringVersion CurrentVersion => ContextVersion;
    }
}

namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Orc.Monitoring.Reporters;
using Orc.Monitoring.Filters;


[TestFixture]
public class MonitoringControllerTests
{
    [SetUp]
    public void Setup()
    {
        MonitoringController.ResetForTesting();
        MonitoringController.Enable(); // Enable monitoring by default for tests
    }

    private void EnableReporter(Type reporterType)
    {
        MonitoringController.EnableReporter(reporterType);
        Assert.That(MonitoringController.IsReporterEnabled(reporterType), Is.True);
    }

    private void DisableReporter(Type reporterType)
    {
        MonitoringController.DisableReporter(reporterType);
        Assert.That(MonitoringController.IsReporterEnabled(reporterType), Is.False);
    }

    private void EnableFilter(Type filterType)
    {
        MonitoringController.EnableFilter(filterType);
        Assert.That(MonitoringController.IsFilterEnabled(filterType), Is.True);
    }

    private void DisableFilter(Type filterType)
    {
        MonitoringController.DisableFilter(filterType);
        Assert.That(MonitoringController.IsFilterEnabled(filterType), Is.False);
    }

    [Test]
    public void EnableReporter_WhenCalled_EnablesReporter()
    {
        EnableReporter(typeof(WorkflowReporter));
    }

    [Test]
    public void DisableReporter_WhenCalled_DisablesReporter()
    {
        EnableReporter(typeof(WorkflowReporter));
        DisableReporter(typeof(WorkflowReporter));
    }

    [Test]
    public void EnableFilter_WhenCalled_EnablesFilter()
    {
        EnableFilter(typeof(WorkflowItemFilter));
    }

    [Test]
    public void DisableFilter_WhenCalled_DisablesFilter()
    {
        EnableFilter(typeof(WorkflowItemFilter));
        DisableFilter(typeof(WorkflowItemFilter));
    }

    [Test]
    public void ShouldTrack_WhenMonitoringEnabledAndReporterEnabled_ReturnsTrue()
    {
        MonitoringController.Enable();
        MonitoringController.EnableReporter(typeof(WorkflowReporter));
        var currentVersion = MonitoringController.GetCurrentVersion();
        Assert.That(MonitoringController.ShouldTrack(currentVersion, typeof(WorkflowReporter)), Is.True);
    }

    [Test]
    public void ShouldTrack_WhenMonitoringDisabled_ReturnsFalse()
    {
        MonitoringController.Disable();
        MonitoringController.EnableReporter(typeof(WorkflowReporter));
        var currentVersion = MonitoringController.GetCurrentVersion();
        Assert.That(MonitoringController.ShouldTrack(currentVersion, typeof(WorkflowReporter)), Is.False);
    }

    [Test]
    public void TemporarilyEnableReporter_EnablesReporterAndRevertOnDispose()
    {
        MonitoringController.DisableReporter(typeof(WorkflowReporter));
        Assert.That(MonitoringController.IsReporterEnabled(typeof(WorkflowReporter)), Is.False);

        using (var temp = MonitoringController.TemporarilyEnableReporter<WorkflowReporter>())
        {
            Assert.That(MonitoringController.IsReporterEnabled(typeof(WorkflowReporter)), Is.True);
        }

        Assert.That(MonitoringController.IsReporterEnabled(typeof(WorkflowReporter)), Is.False);
    }

    [Test]
    public void TemporarilyEnableFilter_EnablesFilterAndRevertOnDispose()
    {
        MonitoringController.DisableFilter(typeof(WorkflowItemFilter));
        Assert.That(MonitoringController.IsFilterEnabled(typeof(WorkflowItemFilter)), Is.False);

        using (var temp = MonitoringController.TemporarilyEnableFilter<WorkflowItemFilter>())
        {
            Assert.That(MonitoringController.IsFilterEnabled(typeof(WorkflowItemFilter)), Is.True);
        }

        Assert.That(MonitoringController.IsFilterEnabled(typeof(WorkflowItemFilter)), Is.False);
    }

    [Test]
    public void GetCurrentVersion_IncreasesAfterStateChange()
    {
        var initialVersion = MonitoringController.GetCurrentVersion();
        MonitoringController.EnableReporter(typeof(WorkflowReporter));
        var newVersion = MonitoringController.GetCurrentVersion();
        Assert.That(newVersion, Is.GreaterThan(initialVersion));
    }

    [Test]
    public void GlobalDisableEnable_RestoresCorrectComponentStates()
    {
        MonitoringController.EnableReporter(typeof(WorkflowReporter));
        MonitoringController.DisableFilter(typeof(WorkflowItemFilter));
        MonitoringController.Disable();
        MonitoringController.Enable();

        Assert.Multiple(() =>
        {
            Assert.That(MonitoringController.IsReporterEnabled(typeof(WorkflowReporter)), Is.True);
            Assert.That(MonitoringController.IsFilterEnabled(typeof(WorkflowItemFilter)), Is.False);
        });
    }

    [Test]
    public void ConcurrentEnableDisable_MaintainsConsistentState()
    {
        var tasks = new List<Task>();
        for (int i = 0; i < 100; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                MonitoringController.EnableReporter(typeof(WorkflowReporter));
                MonitoringController.DisableReporter(typeof(WorkflowReporter));
            }));
        }

        Task.WaitAll(tasks.ToArray());

        Assert.That(MonitoringController.IsReporterEnabled(typeof(WorkflowReporter)), Is.False);
    }

    [Test]
    public void VersionChanged_EventFired_WhenVersionChanges()
    {
        var eventFired = false;
        MonitoringVersion? newVersion = null;

        MonitoringController.VersionChanged += (sender, version) =>
        {
            eventFired = true;
            newVersion = version;
        };

        MonitoringController.EnableReporter(typeof(WorkflowReporter));

        Assert.That(eventFired, Is.True);
        Assert.That(newVersion, Is.Not.Null);
        Assert.That(newVersion, Is.EqualTo(MonitoringController.GetCurrentVersion()));
    }

    [Test]
    public void BeginOperation_ReturnsCorrectVersion()
    {
        using (MonitoringController.BeginOperation(out var operationVersion))
        {
            Assert.That(operationVersion, Is.EqualTo(MonitoringController.GetCurrentVersion()));
        }
    }

    [Test]
    public void Configuration_WhenSet_TriggersVersionChange()
    {
        var initialVersion = MonitoringController.GetCurrentVersion();
        MonitoringController.Configuration = new MonitoringConfiguration();
        var newVersion = MonitoringController.GetCurrentVersion();

        Assert.That(newVersion, Is.GreaterThan(initialVersion));
    }

    [Test]
    public void VersionChange_UpdatesCacheAndNotifiesListeners()
    {
        var versionChangeCount = 0;
        MonitoringController.VersionChanged += (sender, version) => versionChangeCount++;

        var initialVersion = MonitoringController.GetCurrentVersion();
        var reporterType = typeof(WorkflowReporter);

        MonitoringController.EnableReporter(reporterType);
        var afterFirstEnableVersion = MonitoringController.GetCurrentVersion();

        Assert.That(afterFirstEnableVersion, Is.Not.EqualTo(initialVersion));
        Assert.That(MonitoringController.ShouldTrack(afterFirstEnableVersion, reporterType), Is.True);

        MonitoringController.EnableReporter(typeof(PerformanceReporter));
        var finalVersion = MonitoringController.GetCurrentVersion();

        Assert.Multiple(() =>
        {
            Assert.That(finalVersion, Is.Not.EqualTo(afterFirstEnableVersion));
            Assert.That(MonitoringController.ShouldTrack(initialVersion, reporterType), Is.False);
            Assert.That(MonitoringController.ShouldTrack(afterFirstEnableVersion, reporterType), Is.False);
            Assert.That(MonitoringController.ShouldTrack(finalVersion, reporterType), Is.True);
            Assert.That(versionChangeCount, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task LongRunningOperation_HandlesVersionChangesAsync()
    {
        var initialVersion = MonitoringController.GetCurrentVersion();
        var operationTask = Task.Run(async () =>
        {
            using (MonitoringController.BeginOperation(out var operationVersion))
            {
                Assert.That(initialVersion, Is.EqualTo(operationVersion));
                await Task.Delay(1000); // Simulate long-running operation
                return MonitoringController.ShouldTrack(operationVersion, typeof(WorkflowReporter));
            }
        });

        await Task.Delay(200); // Give some time for the operation to start
        MonitoringController.EnableReporter(typeof(PerformanceReporter)); // This should change the version

        var result = await operationTask;
        Assert.That(result, Is.False,  "Long-running operation should not track after version change");
    }

    [Test]
    public void VersionOverflow_HandledGracefully()
    {
        // Set the version to the maximum value
        typeof(MonitoringController).GetField("_currentVersion", BindingFlags.NonPublic | BindingFlags.Static)
            .SetValue(null, new MonitoringVersion(long.MaxValue, Guid.NewGuid()));

        var beforeOverflow = MonitoringController.GetCurrentVersion();
        MonitoringController.EnableReporter(typeof(WorkflowReporter)); // This should increment the version
        var afterOverflow = MonitoringController.GetCurrentVersion();

        Assert.That(afterOverflow.MainVersion, Is.LessThan(beforeOverflow.MainVersion));
        Assert.That(afterOverflow.ChangeId, Is.Not.EqualTo(beforeOverflow.ChangeId));
    }

    [Test]
    public void VersionChange_LogsChangeAndGeneratesReport()
    {
        var initialVersion = MonitoringController.GetCurrentVersion();
        MonitoringController.EnableReporter(typeof(WorkflowReporter));
        var newVersion = MonitoringController.GetCurrentVersion();

        Assert.That(newVersion, Is.Not.EqualTo(initialVersion));

        var report = MonitoringController.GenerateVersionReport();
        Assert.That(report, Does.Contain(initialVersion.ToString()));
        Assert.That(report, Does.Contain(newVersion.ToString()));
    }
}

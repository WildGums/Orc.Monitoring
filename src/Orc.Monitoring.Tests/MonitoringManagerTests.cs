namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using System;
using Orc.Monitoring.Reporters;
using Orc.Monitoring.Filters;


[TestFixture]
public class MonitoringManagerTests
{
    [SetUp]
    public void Setup()
    {
        MonitoringManager.ResetForTesting();
        MonitoringManager.Disable();
    }

    [Test]
    public void EnableReporter_WhenCalled_EnablesReporter()
    {
        MonitoringManager.EnableReporter(typeof(WorkflowReporter));
        Assert.That(MonitoringManager.IsReporterEnabled(typeof(WorkflowReporter)), Is.True);
    }

    [Test]
    public void DisableReporter_WhenCalled_DisablesReporter()
    {
        MonitoringManager.EnableReporter(typeof(WorkflowReporter));
        MonitoringManager.DisableReporter(typeof(WorkflowReporter));
        Assert.That(MonitoringManager.IsReporterEnabled(typeof(WorkflowReporter)), Is.False);
    }

    [Test]
    public void EnableFilter_WhenCalled_EnablesFilter()
    {
        MonitoringManager.EnableFilter(typeof(WorkflowItemFilter));
        Assert.That(MonitoringManager.IsFilterEnabled(typeof(WorkflowItemFilter)), Is.True);
    }

    [Test]
    public void DisableFilter_WhenCalled_DisablesFilter()
    {
        MonitoringManager.EnableFilter(typeof(WorkflowItemFilter));
        MonitoringManager.DisableFilter(typeof(WorkflowItemFilter));
        Assert.That(MonitoringManager.IsFilterEnabled(typeof(WorkflowItemFilter)), Is.False);
    }

    [Test]
    public void ShouldTrack_WhenMonitoringEnabledAndReporterEnabled_ReturnsTrue()
    {
        MonitoringManager.Enable();
        MonitoringManager.EnableReporter(typeof(WorkflowReporter));
        Assert.That(MonitoringManager.ShouldTrack(MonitoringManager.CurrentVersion, typeof(WorkflowReporter)), Is.True);
    }

    [Test]
    public void ShouldTrack_WhenMonitoringDisabled_ReturnsFalse()
    {
        MonitoringManager.Disable();
        MonitoringManager.EnableReporter(typeof(WorkflowReporter));
        Assert.That(MonitoringManager.ShouldTrack(MonitoringManager.CurrentVersion, typeof(WorkflowReporter)), Is.False);
    }

    [Test]
    public void TemporarilyEnableReporter_EnablesReporterAndRevertOnDispose()
    {
        MonitoringManager.DisableReporter(typeof(WorkflowReporter));
        Assert.That(MonitoringManager.IsReporterEnabled(typeof(WorkflowReporter)), Is.False);

        using (var temp = MonitoringManager.TemporarilyEnableReporter(typeof(WorkflowReporter)))
        {
            Assert.That(MonitoringManager.IsReporterEnabled(typeof(WorkflowReporter)), Is.True);
        }

        Assert.That(MonitoringManager.IsReporterEnabled(typeof(WorkflowReporter)), Is.False);
    }

    [Test]
    public void TemporarilyEnableFilter_EnablesFilterAndRevertOnDispose()
    {
        MonitoringManager.DisableFilter(typeof(WorkflowItemFilter));
        Assert.That(MonitoringManager.IsFilterEnabled(typeof(WorkflowItemFilter)), Is.False);

        using (var temp = MonitoringManager.TemporarilyEnableFilter(typeof(WorkflowItemFilter)))
        {
            Assert.That(MonitoringManager.IsFilterEnabled(typeof(WorkflowItemFilter)), Is.True);
        }

        Assert.That(MonitoringManager.IsFilterEnabled(typeof(WorkflowItemFilter)), Is.False);
    }

    [Test]
    public void TemporarilyEnableReporter_DoesNotAffectOtherReporters()
    {
        MonitoringManager.DisableReporter(typeof(WorkflowReporter));
        MonitoringManager.EnableReporter(typeof(PerformanceReporter));

        using (var temp = MonitoringManager.TemporarilyEnableReporter(typeof(WorkflowReporter)))
        {
            Assert.That(MonitoringManager.IsReporterEnabled(typeof(WorkflowReporter)), Is.True);
            Assert.That(MonitoringManager.IsReporterEnabled(typeof(PerformanceReporter)), Is.True);
        }

        Assert.That(MonitoringManager.IsReporterEnabled(typeof(WorkflowReporter)), Is.False);
        Assert.That(MonitoringManager.IsReporterEnabled(typeof(PerformanceReporter)), Is.True);
    }

    [Test]
    public void TemporarilyEnableFilter_DoesNotAffectOtherFilters()
    {
        MonitoringManager.DisableFilter(typeof(WorkflowItemFilter));
        MonitoringManager.EnableFilter(typeof(PerformanceFilter));

        using (var temp = MonitoringManager.TemporarilyEnableFilter(typeof(WorkflowItemFilter)))
        {
            Assert.That(MonitoringManager.IsFilterEnabled(typeof(WorkflowItemFilter)), Is.True);
            Assert.That(MonitoringManager.IsFilterEnabled(typeof(PerformanceFilter)), Is.True);
        }

        Assert.That(MonitoringManager.IsFilterEnabled(typeof(WorkflowItemFilter)), Is.False);
        Assert.That(MonitoringManager.IsFilterEnabled(typeof(PerformanceFilter)), Is.True);
    }

    [Test]
    public void TemporarilyEnableReporter_NestedCalls_WorkCorrectly()
    {
        MonitoringManager.DisableReporter(typeof(WorkflowReporter));
        Assert.That(MonitoringManager.IsReporterEnabled(typeof(WorkflowReporter)), Is.False);

        using (var outer = MonitoringManager.TemporarilyEnableReporter(typeof(WorkflowReporter)))
        {
            Assert.That(MonitoringManager.IsReporterEnabled(typeof(WorkflowReporter)), Is.True);

            using (var inner = MonitoringManager.TemporarilyEnableReporter(typeof(WorkflowReporter)))
            {
                Assert.That(MonitoringManager.IsReporterEnabled(typeof(WorkflowReporter)), Is.True);
            }

            Assert.That(MonitoringManager.IsReporterEnabled(typeof(WorkflowReporter)), Is.True);
        }

        Assert.That(MonitoringManager.IsReporterEnabled(typeof(WorkflowReporter)), Is.False);
    }

    [Test]
    public void TemporarilyEnableFilter_NestedCalls_WorkCorrectly()
    {
        MonitoringManager.DisableFilter(typeof(WorkflowItemFilter));
        Assert.That(MonitoringManager.IsFilterEnabled(typeof(WorkflowItemFilter)), Is.False);

        using (var outer = MonitoringManager.TemporarilyEnableFilter(typeof(WorkflowItemFilter)))
        {
            Assert.That(MonitoringManager.IsFilterEnabled(typeof(WorkflowItemFilter)), Is.True);

            using (var inner = MonitoringManager.TemporarilyEnableFilter(typeof(WorkflowItemFilter)))
            {
                Assert.That(MonitoringManager.IsFilterEnabled(typeof(WorkflowItemFilter)), Is.True);
            }

            Assert.That(MonitoringManager.IsFilterEnabled(typeof(WorkflowItemFilter)), Is.True);
        }

        Assert.That(MonitoringManager.IsFilterEnabled(typeof(WorkflowItemFilter)), Is.False);
    }

    [Test]
    public void ShouldTrack_WhenReporterDisabledButFilterEnabled_ReturnsFalse()
    {
        MonitoringManager.Enable();
        MonitoringManager.DisableReporter(typeof(WorkflowReporter));
        MonitoringManager.EnableFilter(typeof(WorkflowItemFilter));
        Assert.That(MonitoringManager.ShouldTrack(MonitoringManager.CurrentVersion, typeof(WorkflowReporter), typeof(WorkflowItemFilter)), Is.False);
    }

    [Test]
    public void ShouldTrack_WhenReporterEnabledButFilterDisabled_ReturnsFalse()
    {
        MonitoringManager.Enable();
        MonitoringManager.EnableReporter(typeof(WorkflowReporter));
        MonitoringManager.DisableFilter(typeof(WorkflowItemFilter));
        Assert.That(MonitoringManager.ShouldTrack(MonitoringManager.CurrentVersion, typeof(WorkflowReporter), typeof(WorkflowItemFilter)), Is.False);
    }

    [Test]
    public void GetCurrentVersion_IncreasesAfterStateChange()
    {
        MonitoringManager.ResetForTesting();
        int initialVersion = MonitoringManager.GetCurrentVersion();
        MonitoringManager.EnableReporter(typeof(WorkflowReporter));
        int newVersion = MonitoringManager.GetCurrentVersion();
        Assert.That(newVersion, Is.GreaterThan(initialVersion));
    }
}

namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using System;
using System.Collections.Generic;
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

    [Test]
    public void EnableReporter_WhenCalled_EnablesReporter()
    {
        MonitoringController.EnableReporter(typeof(WorkflowReporter));
        Assert.That(MonitoringController.IsReporterEnabled(typeof(WorkflowReporter)), Is.True);
    }

    [Test]
    public void DisableReporter_WhenCalled_DisablesReporter()
    {
        MonitoringController.EnableReporter(typeof(WorkflowReporter));
        MonitoringController.DisableReporter(typeof(WorkflowReporter));
        Assert.That(MonitoringController.IsReporterEnabled(typeof(WorkflowReporter)), Is.False);
    }

    [Test]
    public void EnableFilter_WhenCalled_EnablesFilter()
    {
        MonitoringController.EnableFilter(typeof(WorkflowItemFilter));
        Assert.That(MonitoringController.IsFilterEnabled(typeof(WorkflowItemFilter)), Is.True);
    }

    [Test]
    public void DisableFilter_WhenCalled_DisablesFilter()
    {
        MonitoringController.EnableFilter(typeof(WorkflowItemFilter));
        MonitoringController.DisableFilter(typeof(WorkflowItemFilter));
        Assert.That(MonitoringController.IsFilterEnabled(typeof(WorkflowItemFilter)), Is.False);
    }

    [Test]
    public void ShouldTrack_WhenMonitoringEnabledAndReporterEnabled_ReturnsTrue()
    {
        MonitoringController.Enable();
        MonitoringController.EnableReporter(typeof(WorkflowReporter));
        Assert.That(MonitoringController.ShouldTrack(MonitoringController.CurrentVersion, typeof(WorkflowReporter)), Is.True);
    }

    [Test]
    public void ShouldTrack_WhenMonitoringDisabled_ReturnsFalse()
    {
        MonitoringController.Disable();
        MonitoringController.EnableReporter(typeof(WorkflowReporter));
        Assert.That(MonitoringController.ShouldTrack(MonitoringController.CurrentVersion, typeof(WorkflowReporter)), Is.False);
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
    public void TemporarilyEnableReporter_DoesNotAffectOtherReporters()
    {
        MonitoringController.DisableReporter(typeof(WorkflowReporter));
        MonitoringController.EnableReporter(typeof(PerformanceReporter));

        using (var temp = MonitoringController.TemporarilyEnableReporter<WorkflowReporter>())
        {
            Assert.That(MonitoringController.IsReporterEnabled(typeof(WorkflowReporter)), Is.True);
            Assert.That(MonitoringController.IsReporterEnabled(typeof(PerformanceReporter)), Is.True);
        }

        Assert.That(MonitoringController.IsReporterEnabled(typeof(WorkflowReporter)), Is.False);
        Assert.That(MonitoringController.IsReporterEnabled(typeof(PerformanceReporter)), Is.True);
    }

    [Test]
    public void TemporarilyEnableFilter_DoesNotAffectOtherFilters()
    {
        MonitoringController.DisableFilter(typeof(WorkflowItemFilter));
        MonitoringController.EnableFilter(typeof(PerformanceFilter));

        using (var temp = MonitoringController.TemporarilyEnableFilter<WorkflowItemFilter>())
        {
            Assert.That(MonitoringController.IsFilterEnabled(typeof(WorkflowItemFilter)), Is.True);
            Assert.That(MonitoringController.IsFilterEnabled(typeof(PerformanceFilter)), Is.True);
        }

        Assert.That(MonitoringController.IsFilterEnabled(typeof(WorkflowItemFilter)), Is.False);
        Assert.That(MonitoringController.IsFilterEnabled(typeof(PerformanceFilter)), Is.True);
    }

    [Test]
    public void TemporarilyEnableReporter_NestedCalls_WorkCorrectly()
    {
        MonitoringController.DisableReporter(typeof(WorkflowReporter));
        Assert.That(MonitoringController.IsReporterEnabled(typeof(WorkflowReporter)), Is.False);

        using (var outer = MonitoringController.TemporarilyEnableReporter<WorkflowReporter>())
        {
            Assert.That(MonitoringController.IsReporterEnabled(typeof(WorkflowReporter)), Is.True);

            using (var inner = MonitoringController.TemporarilyEnableReporter<WorkflowReporter>())
            {
                Assert.That(MonitoringController.IsReporterEnabled(typeof(WorkflowReporter)), Is.True);
            }

            Assert.That(MonitoringController.IsReporterEnabled(typeof(WorkflowReporter)), Is.True);
        }

        Assert.That(MonitoringController.IsReporterEnabled(typeof(WorkflowReporter)), Is.False);
    }

    [Test]
    public void TemporarilyEnableFilter_NestedCalls_WorkCorrectly()
    {
        MonitoringController.DisableFilter(typeof(WorkflowItemFilter));
        Assert.That(MonitoringController.IsFilterEnabled(typeof(WorkflowItemFilter)), Is.False);

        using (var outer = MonitoringController.TemporarilyEnableFilter<WorkflowItemFilter>())
        {
            Assert.That(MonitoringController.IsFilterEnabled(typeof(WorkflowItemFilter)), Is.True);

            using (var inner = MonitoringController.TemporarilyEnableFilter<WorkflowItemFilter>())
            {
                Assert.That(MonitoringController.IsFilterEnabled(typeof(WorkflowItemFilter)), Is.True);
            }

            Assert.That(MonitoringController.IsFilterEnabled(typeof(WorkflowItemFilter)), Is.True);
        }

        Assert.That(MonitoringController.IsFilterEnabled(typeof(WorkflowItemFilter)), Is.False);
    }

    [Test]
    public void ShouldTrack_WhenReporterDisabledButFilterEnabled_ReturnsFalse()
    {
        MonitoringController.Enable();
        MonitoringController.DisableReporter(typeof(WorkflowReporter));
        MonitoringController.EnableFilter(typeof(WorkflowItemFilter));

        // Add debugging output
        Console.WriteLine($"Is Enabled: {MonitoringController.IsEnabled}");
        Console.WriteLine($"Reporter Enabled: {MonitoringController.IsReporterEnabled(typeof(WorkflowReporter))}");
        Console.WriteLine($"Filter Enabled: {MonitoringController.IsFilterEnabled(typeof(WorkflowItemFilter))}");
        Console.WriteLine($"Current Version: {MonitoringController.CurrentVersion}");

        var result = MonitoringController.ShouldTrack(MonitoringController.CurrentVersion, typeof(WorkflowReporter), typeof(WorkflowItemFilter));
        Console.WriteLine($"ShouldTrack Result: {result}");

        Assert.That(result, Is.False);
    }

    [Test]
    public void ShouldTrack_WhenReporterEnabledButFilterDisabled_ReturnsFalse()
    {
        MonitoringController.Enable();
        MonitoringController.EnableReporter(typeof(WorkflowReporter));
        MonitoringController.DisableFilter(typeof(WorkflowItemFilter));

        // Add debugging output
        Console.WriteLine($"Is Enabled: {MonitoringController.IsEnabled}");
        Console.WriteLine($"Reporter Enabled: {MonitoringController.IsReporterEnabled(typeof(WorkflowReporter))}");
        Console.WriteLine($"Filter Enabled: {MonitoringController.IsFilterEnabled(typeof(WorkflowItemFilter))}");
        Console.WriteLine($"Current Version: {MonitoringController.CurrentVersion}");

        var result = MonitoringController.ShouldTrack(MonitoringController.CurrentVersion, typeof(WorkflowReporter), typeof(WorkflowItemFilter));
        Console.WriteLine($"ShouldTrack Result: {result}");

        Assert.That(result, Is.False);
    }

    [Test]
    public void GetCurrentVersion_IncreasesAfterStateChange()
    {
        MonitoringController.ResetForTesting();
        int initialVersion = MonitoringController.GetCurrentVersion();
        MonitoringController.EnableReporter(typeof(WorkflowReporter));
        int newVersion = MonitoringController.GetCurrentVersion();
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
    public void NestedTemporaryEnables_WorkCorrectly()
    {
        MonitoringController.DisableReporter(typeof(WorkflowReporter));

        using (MonitoringController.TemporarilyEnableReporter<WorkflowReporter>())
        {
            Assert.That(MonitoringController.IsReporterEnabled(typeof(WorkflowReporter)), Is.True);

            using (MonitoringController.TemporarilyEnableReporter<WorkflowReporter>())
            {
                Assert.That(MonitoringController.IsReporterEnabled(typeof(WorkflowReporter)), Is.True);
            }

            Assert.That(MonitoringController.IsReporterEnabled(typeof(WorkflowReporter)), Is.True);
        }

        Assert.That(MonitoringController.IsReporterEnabled(typeof(WorkflowReporter)), Is.False);
    }

    [Test]
    public void GlobalStateChanges_AffectComponentStates()
    {
        MonitoringController.EnableReporter(typeof(WorkflowReporter));
        MonitoringController.EnableFilter(typeof(WorkflowItemFilter));

        MonitoringController.Disable();

        Assert.Multiple(() =>
        {
            Assert.That(MonitoringController.IsEnabled, Is.False);
            Assert.That(MonitoringController.IsReporterEnabled(typeof(WorkflowReporter)), Is.False);
            Assert.That(MonitoringController.IsFilterEnabled(typeof(WorkflowItemFilter)), Is.False);
        });

        MonitoringController.Enable();

        Assert.Multiple(() =>
        {
            Assert.That(MonitoringController.IsEnabled, Is.True);
            Assert.That(MonitoringController.IsReporterEnabled(typeof(WorkflowReporter)), Is.True);
            Assert.That(MonitoringController.IsFilterEnabled(typeof(WorkflowItemFilter)), Is.True);
        });
    }

    [Test]
    public void HierarchicalControl_ComponentStatesRespectGlobalState()
    {
        MonitoringController.EnableReporter(typeof(WorkflowReporter));
        MonitoringController.EnableFilter(typeof(WorkflowItemFilter));

        MonitoringController.Disable();

        Assert.That(MonitoringController.ShouldTrack(MonitoringController.CurrentVersion, typeof(WorkflowReporter), typeof(WorkflowItemFilter)), Is.False);

        MonitoringController.Enable();

        Assert.That(MonitoringController.ShouldTrack(MonitoringController.CurrentVersion, typeof(WorkflowReporter), typeof(WorkflowItemFilter)), Is.True);
    }

    [Test]
    public void TemporaryStateChanges_NestedScenarios_WorkCorrectly()
    {
        MonitoringController.DisableReporter(typeof(WorkflowReporter));

        using (MonitoringController.TemporarilyEnableReporter<WorkflowReporter>())
        {
            Assert.That(MonitoringController.IsReporterEnabled(typeof(WorkflowReporter)), Is.True);

            using (MonitoringController.TemporarilyEnableReporter<WorkflowReporter>())
            {
                Assert.That(MonitoringController.IsReporterEnabled(typeof(WorkflowReporter)), Is.True);
            }

            Assert.That(MonitoringController.IsReporterEnabled(typeof(WorkflowReporter)), Is.True);
        }

        Assert.That(MonitoringController.IsReporterEnabled(typeof(WorkflowReporter)), Is.False);
    }

    [Test]
    public void CallbackSystem_NotifiesOnStateChanges()
    {
        var callbackCalled = false;
        MonitoringController.AddStateChangedCallback((componentType, componentName, isEnabled, version) =>
        {
            callbackCalled = true;
        });

        MonitoringController.EnableReporter(typeof(WorkflowReporter));

        Assert.That(callbackCalled, Is.True);
    }

    [Test]
    public void ConcurrentAccess_MaintainsConsistentState()
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
    public void RapidStateToggling_MaintainsCorrectFinalState()
    {
        for (int i = 0; i < 100; i++)
        {
            MonitoringController.EnableReporter(typeof(WorkflowReporter));
            MonitoringController.DisableReporter(typeof(WorkflowReporter));
        }

        Assert.That(MonitoringController.IsReporterEnabled(typeof(WorkflowReporter)), Is.False);

        MonitoringController.EnableReporter(typeof(WorkflowReporter));

        Assert.That(MonitoringController.IsReporterEnabled(typeof(WorkflowReporter)), Is.True);
    }
}

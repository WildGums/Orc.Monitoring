namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using MethodLifeCycleItems;
using Orc.Monitoring.Reporters;
using Orc.Monitoring.Filters;
using Reporters.ReportOutputs;

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

        var result = MonitoringController.ShouldTrack(MonitoringController.CurrentVersion, typeof(WorkflowReporter), typeof(WorkflowItemFilter));
        Assert.That(result, Is.False);
    }

    [Test]
    public void ShouldTrack_WhenReporterEnabledButFilterDisabled_ReturnsFalse()
    {
        MonitoringController.Enable();
        MonitoringController.EnableReporter(typeof(WorkflowReporter));
        MonitoringController.DisableFilter(typeof(WorkflowItemFilter));

        var result = MonitoringController.ShouldTrack(MonitoringController.CurrentVersion, typeof(WorkflowReporter), typeof(WorkflowItemFilter));
        Assert.That(result, Is.False);
    }

    [Test]
    public void ShouldTrack_ConsidersAllFactors()
    {
        MonitoringController.Enable();
        MonitoringController.EnableReporter(typeof(WorkflowReporter));
        MonitoringController.EnableFilter(typeof(WorkflowItemFilter));

        Assert.That(MonitoringController.ShouldTrack(MonitoringController.CurrentVersion, typeof(WorkflowReporter), typeof(WorkflowItemFilter)), Is.True);

        MonitoringController.DisableFilter(typeof(WorkflowItemFilter));
        Assert.That(MonitoringController.ShouldTrack(MonitoringController.CurrentVersion, typeof(WorkflowReporter), typeof(WorkflowItemFilter)), Is.False);

        MonitoringController.Disable();
        Assert.That(MonitoringController.ShouldTrack(MonitoringController.CurrentVersion, typeof(WorkflowReporter), typeof(WorkflowItemFilter)), Is.False);
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

        // After all concurrent operations, the state should be consistent
        // We expect it to be disabled because the last operation in each task is Disable
        Assert.That(MonitoringController.IsReporterEnabled(typeof(WorkflowReporter)), Is.False);
    }

    [Test]
    public void ConcurrentEnable_EnsuresReporterIsEnabled()
    {
        var tasks = new List<Task>();
        for (int i = 0; i < 100; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                MonitoringController.EnableReporter(typeof(WorkflowReporter));
            }));
        }

        Task.WaitAll(tasks.ToArray());

        Assert.That(MonitoringController.IsReporterEnabled(typeof(WorkflowReporter)), Is.True);
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
    public void HierarchicalControl_GlobalStateOverridesComponentStates()
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

    [Test]
    public void GetAllComponentStates_ReturnsCorrectStates()
    {
        MonitoringController.EnableReporter(typeof(WorkflowReporter));
        MonitoringController.DisableFilter(typeof(WorkflowItemFilter));

        var states = MonitoringController.GetAllComponentStates();

        Assert.That(states["Reporter:WorkflowReporter"], Is.True);
        Assert.That(states["Filter:WorkflowItemFilter"], Is.False);
    }

    [Test]
    public void RegisterCustomComponent_AddsComponentCorrectly()
    {
        var customReporterType = typeof(CustomReporter);
        MonitoringController.RegisterCustomComponent(customReporterType, MonitoringController.MonitoringComponentType.Reporter);

        MonitoringController.EnableReporter(customReporterType);
        Assert.That(MonitoringController.IsReporterEnabled(customReporterType), Is.True);
    }

    [Test]
    public void NonExistentComponents_DoNotCauseErrors()
    {
        Assert.DoesNotThrow(() => MonitoringController.EnableReporter(typeof(string)));
        Assert.DoesNotThrow(() => MonitoringController.DisableFilter(typeof(int)));
    }

    [Test]
    public void StateChange_CompletesWithinAcceptableTime()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        MonitoringController.EnableReporter(typeof(WorkflowReporter));

        stopwatch.Stop();

        Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(10), "State change took too long");
    }

    private class CustomReporter : IMethodCallReporter
    {
        public string Name => "CustomReporter";
        public string FullName => "Orc.Monitoring.Tests.CustomReporter";
        public MethodInfo? RootMethod { get; set; }

        public IAsyncDisposable StartReporting(IObservable<ICallStackItem> callStack)
        {
            // For testing purposes, we'll just return a simple AsyncDisposable
            return new AsyncDisposable(async () => { });
        }

        public IOutputContainer AddOutput<TOutput>(object? parameter = null) where TOutput : IReportOutput, new()
        {
            // For testing, we'll just return this instance
            return this;
        }
    }
}

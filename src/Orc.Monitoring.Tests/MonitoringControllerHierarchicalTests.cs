namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using Filters;
using Reporters;
using System.Collections.Generic;
using System.Threading.Tasks;

[TestFixture]
public class MonitoringControllerHierarchicalTests
{
    [SetUp]
    public void Setup()
    {
        MonitoringController.ResetForTesting();
    }

    [Test]
    public void GlobalDisable_DisablesAllComponents()
    {
        MonitoringController.Enable();
        MonitoringController.EnableReporter(typeof(TestWorkflowReporter));
        MonitoringController.EnableFilter(typeof(WorkflowItemFilter));

        MonitoringController.Disable();

        Assert.Multiple(() =>
        {
            Assert.That(MonitoringController.IsEnabled, Is.False);
            Assert.That(MonitoringController.IsReporterEnabled(typeof(TestWorkflowReporter)), Is.False);
            Assert.That(MonitoringController.IsFilterEnabled(typeof(WorkflowItemFilter)), Is.False);
        });
    }

    [Test]
    public void GlobalEnable_RestoresComponentStates()
    {
        MonitoringController.Enable();
        MonitoringController.EnableReporter(typeof(TestWorkflowReporter));
        MonitoringController.DisableFilter(typeof(WorkflowItemFilter));
        MonitoringController.Disable();

        MonitoringController.Enable();

        Assert.Multiple(() =>
        {
            Assert.That(MonitoringController.IsEnabled, Is.True);
            Assert.That(MonitoringController.IsReporterEnabled(typeof(TestWorkflowReporter)), Is.True);
            Assert.That(MonitoringController.IsFilterEnabled(typeof(WorkflowItemFilter)), Is.False);
        });
    }

    [Test]
    public void ShouldTrack_RespectesHierarchy()
    {
        // Arrange
        MonitoringController.Enable();
        MonitoringController.EnableReporter(typeof(TestWorkflowReporter));
        MonitoringController.EnableFilter(typeof(WorkflowItemFilter));
        MonitoringController.EnableFilterForReporterType(typeof(TestWorkflowReporter), typeof(WorkflowItemFilter));

        var currentVersion = MonitoringController.GetCurrentVersion();

        // Act & Assert
        Assert.That(MonitoringController.ShouldTrack(currentVersion, typeof(TestWorkflowReporter), typeof(WorkflowItemFilter)), Is.True, "All components should be enabled");

        MonitoringController.Disable();

        Assert.That(MonitoringController.ShouldTrack(currentVersion, typeof(TestWorkflowReporter), typeof(WorkflowItemFilter)), Is.False, "All components should be disabled when globally disabled");

        MonitoringController.Enable();
        MonitoringController.DisableReporter(typeof(TestWorkflowReporter));

        Assert.That(MonitoringController.ShouldTrack(currentVersion, typeof(TestWorkflowReporter), typeof(WorkflowItemFilter)), Is.False, "Should not track when reporter is disabled");

        MonitoringController.EnableReporter(typeof(TestWorkflowReporter));
        MonitoringController.DisableFilter(typeof(WorkflowItemFilter));

        Assert.That(MonitoringController.ShouldTrack(currentVersion, typeof(TestWorkflowReporter), typeof(WorkflowItemFilter)), Is.False, "Should not track when filter is disabled");
    }

    [Test]
    public void ComponentStateChanges_DontAffectGlobalState()
    {
        MonitoringController.Enable();
        MonitoringController.DisableReporter(typeof(TestWorkflowReporter));

        Assert.That(MonitoringController.IsEnabled, Is.True);
    }

    [Test]
    public void ConcurrentAccess_MaintainsConsistency()
    {
        MonitoringController.Enable();

        var tasks = new List<Task>();
        for (int i = 0; i < 100; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                MonitoringController.EnableReporter(typeof(TestWorkflowReporter));
                MonitoringController.DisableReporter(typeof(TestWorkflowReporter));
            }));
        }

        Task.WaitAll(tasks.ToArray());

        Assert.That(MonitoringController.IsEnabled, Is.True);
    }
}

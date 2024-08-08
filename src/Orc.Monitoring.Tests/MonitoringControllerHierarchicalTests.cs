namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using Orc.Monitoring.Filters;
using Orc.Monitoring.Reporters;
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
        MonitoringController.EnableReporter(typeof(WorkflowReporter));
        MonitoringController.EnableFilter(typeof(WorkflowItemFilter));

        MonitoringController.Disable();

        Assert.Multiple(() =>
        {
            Assert.That(MonitoringController.IsEnabled, Is.False);
            Assert.That(MonitoringController.IsReporterEnabled(typeof(WorkflowReporter)), Is.False);
            Assert.That(MonitoringController.IsFilterEnabled(typeof(WorkflowItemFilter)), Is.False);
        });
    }

    [Test]
    public void GlobalEnable_RestoresComponentStates()
    {
        MonitoringController.Enable();
        MonitoringController.EnableReporter(typeof(WorkflowReporter));
        MonitoringController.DisableFilter(typeof(WorkflowItemFilter));
        MonitoringController.Disable();

        MonitoringController.Enable();

        Assert.Multiple(() =>
        {
            Assert.That(MonitoringController.IsEnabled, Is.True);
            Assert.That(MonitoringController.IsReporterEnabled(typeof(WorkflowReporter)), Is.True);
            Assert.That(MonitoringController.IsFilterEnabled(typeof(WorkflowItemFilter)), Is.False);
        });
    }

    [Test]
    public void ShouldTrack_RespectesHierarchy()
    {
        MonitoringController.Enable();
        MonitoringController.EnableReporter(typeof(WorkflowReporter));
        MonitoringController.EnableFilter(typeof(WorkflowItemFilter));

        Assert.That(MonitoringController.ShouldTrack(MonitoringController.GetCurrentVersion(), typeof(WorkflowReporter), typeof(WorkflowItemFilter)), Is.True);

        MonitoringController.Disable();

        Assert.That(MonitoringController.ShouldTrack(MonitoringController.GetCurrentVersion(), typeof(WorkflowReporter), typeof(WorkflowItemFilter)), Is.False);
    }

    [Test]
    public void ComponentStateChanges_DontAffectGlobalState()
    {
        MonitoringController.Enable();
        MonitoringController.DisableReporter(typeof(WorkflowReporter));

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
                MonitoringController.EnableReporter(typeof(WorkflowReporter));
                MonitoringController.DisableReporter(typeof(WorkflowReporter));
            }));
        }

        Task.WaitAll(tasks.ToArray());

        Assert.That(MonitoringController.IsEnabled, Is.True);
    }
}

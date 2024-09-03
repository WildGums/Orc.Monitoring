namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using Filters;
using Reporters;
using System.Collections.Generic;
using System.Threading.Tasks;

[TestFixture]
public class MonitoringControllerHierarchicalTests
{
    private TestLogger<MonitoringControllerHierarchicalTests> _logger;
    private TestLoggerFactory<MonitoringControllerHierarchicalTests> _loggerFactory;
    private IMonitoringController _monitoringController;

    [SetUp]
    public void Setup()
    {
        _logger = new TestLogger<MonitoringControllerHierarchicalTests>();
        _loggerFactory = new TestLoggerFactory<MonitoringControllerHierarchicalTests>(_logger);
        _monitoringController = new MonitoringController(_loggerFactory, () => new EnhancedDataPostProcessor(_loggerFactory));
    }

    [Test]
    public void GlobalDisable_DisablesAllComponents()
    {
        _monitoringController.Enable();
        _monitoringController.EnableReporter(typeof(TestWorkflowReporter));
        _monitoringController.EnableFilter(typeof(WorkflowItemFilter));

        _monitoringController.Disable();

        Assert.Multiple(() =>
        {
            Assert.That(_monitoringController.IsEnabled, Is.False);
            Assert.That(_monitoringController.IsReporterEnabled(typeof(TestWorkflowReporter)), Is.False);
            Assert.That(_monitoringController.IsFilterEnabled(typeof(WorkflowItemFilter)), Is.False);
        });
    }

    [Test]
    public void GlobalEnable_RestoresComponentStates()
    {
        _monitoringController.Enable();
        _monitoringController.EnableReporter(typeof(TestWorkflowReporter));
        _monitoringController.DisableFilter(typeof(WorkflowItemFilter));
        _monitoringController.Disable();

        _monitoringController.Enable();

        Assert.Multiple(() =>
        {
            Assert.That(_monitoringController.IsEnabled, Is.True);
            Assert.That(_monitoringController.IsReporterEnabled(typeof(TestWorkflowReporter)), Is.True);
            Assert.That(_monitoringController.IsFilterEnabled(typeof(WorkflowItemFilter)), Is.False);
        });
    }

    [Test]
    public void ShouldTrack_RespectesHierarchy()
    {
        // Arrange
        _monitoringController.Enable();
        _monitoringController.EnableReporter(typeof(TestWorkflowReporter));
        _monitoringController.EnableFilter(typeof(WorkflowItemFilter));
        _monitoringController.EnableFilterForReporterType(typeof(TestWorkflowReporter), typeof(WorkflowItemFilter));

        var currentVersion = _monitoringController.GetCurrentVersion();

        // Act & Assert
        Assert.That(_monitoringController.ShouldTrack(currentVersion, typeof(TestWorkflowReporter), typeof(WorkflowItemFilter)), Is.True, "All components should be enabled");

        _monitoringController.Disable();

        Assert.That(_monitoringController.ShouldTrack(currentVersion, typeof(TestWorkflowReporter), typeof(WorkflowItemFilter)), Is.False, "All components should be disabled when globally disabled");

        _monitoringController.Enable();
        _monitoringController.DisableReporter(typeof(TestWorkflowReporter));

        Assert.That(_monitoringController.ShouldTrack(currentVersion, typeof(TestWorkflowReporter), typeof(WorkflowItemFilter)), Is.False, "Should not track when reporter is disabled");

        _monitoringController.EnableReporter(typeof(TestWorkflowReporter));
        _monitoringController.DisableFilter(typeof(WorkflowItemFilter));

        Assert.That(_monitoringController.ShouldTrack(currentVersion, typeof(TestWorkflowReporter), typeof(WorkflowItemFilter)), Is.False, "Should not track when filter is disabled");
    }

    [Test]
    public void ComponentStateChanges_DontAffectGlobalState()
    {
        _monitoringController.Enable();
        _monitoringController.DisableReporter(typeof(TestWorkflowReporter));

        Assert.That(_monitoringController.IsEnabled, Is.True);
    }

    [Test]
    public void ConcurrentAccess_MaintainsConsistency()
    {
        _monitoringController.Enable();

        var tasks = new List<Task>();
        for (int i = 0; i < 100; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                _monitoringController.EnableReporter(typeof(TestWorkflowReporter));
                _monitoringController.DisableReporter(typeof(TestWorkflowReporter));
            }));
        }

        Task.WaitAll(tasks.ToArray());

        Assert.That(_monitoringController.IsEnabled, Is.True);
    }
}

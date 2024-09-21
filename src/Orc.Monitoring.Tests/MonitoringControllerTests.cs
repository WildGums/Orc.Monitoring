namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Core.Abstractions;
using Core.Configuration;
using Core.Controllers;
using Core.Models;
using Moq;
using Filters;
using Microsoft.Extensions.Logging;
using TestUtilities.Mocks;
using TestUtilities.TestHelpers;
using Utilities.Logging;

[TestFixture]
public class MonitoringControllerTests
{
    private Mock<IMonitoringLoggerFactory> _mockLoggerFactory;
    private Mock<ILogger<MonitoringController>> _mockLogger;
    private MonitoringController _controller;

    [SetUp]
    public void Setup()
    {
        _mockLoggerFactory = new Mock<IMonitoringLoggerFactory>();
        _mockLogger = new Mock<ILogger<MonitoringController>>();

        _mockLoggerFactory.Setup(f => f.CreateLogger(It.IsAny<Type>())).Returns(_mockLogger.Object);

        _controller = new MonitoringController(_mockLoggerFactory.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _controller.Disable();
    }

    [Test]
    public void Enable_SetsIsEnabledToTrue()
    {
        // Act
        _controller.Enable();

        // Assert
        Assert.That(_controller.IsEnabled, Is.True);
    }

    [Test]
    public void Disable_SetsIsEnabledToFalse()
    {
        // Arrange
        _controller.Enable();

        // Act
        _controller.Disable();

        // Assert
        Assert.That(_controller.IsEnabled, Is.False);
    }

    [Test]
    public void EnableReporter_WhenCalled_EnablesReporter()
    {
        // Arrange
        var reporterType = typeof(TestWorkflowReporter);
        _controller.Enable(); // Monitoring must be enabled to enable reporters

        // Act
        _controller.EnableReporter(reporterType);

        // Assert
        Assert.That(_controller.IsReporterEnabled(reporterType), Is.True);
    }

    [Test]
    public void DisableReporter_WhenCalled_DisablesReporter()
    {
        // Arrange
        var reporterType = typeof(TestWorkflowReporter);
        _controller.EnableReporter(reporterType);

        // Act
        _controller.DisableReporter(reporterType);

        // Assert
        Assert.That(_controller.IsReporterEnabled(reporterType), Is.False);
    }

    [Test]
    public void EnableFilter_WhenCalled_EnablesFilter()
    {
        // Arrange
        var filterType = typeof(WorkflowItemFilter);
        _controller.Enable(); // Monitoring must be enabled to enable filters

        // Act
        _controller.EnableFilter(filterType);

        // Assert
        Assert.That(_controller.IsFilterEnabled(filterType), Is.True);
    }

    [Test]
    public void DisableFilter_WhenCalled_DisablesFilter()
    {
        // Arrange
        var filterType = typeof(WorkflowItemFilter);
        _controller.EnableFilter(filterType);

        // Act
        _controller.DisableFilter(filterType);

        // Assert
        Assert.That(_controller.IsFilterEnabled(filterType), Is.False);
    }

    [TestCase(true, true, ExpectedResult = true)]
    [TestCase(false, true, ExpectedResult = false)]
    [TestCase(true, false, ExpectedResult = false)]
    [TestCase(false, false, ExpectedResult = false)]
    public bool ShouldTrack_ReturnsExpectedResult(bool monitoringEnabled, bool reporterEnabled)
    {
        // Arrange
        var reporterType = typeof(TestWorkflowReporter);
        var reporterId = "TestReporter";

        if (monitoringEnabled)
            _controller.Enable();
        else
            _controller.Disable();

        if (reporterEnabled)
            _controller.EnableReporter(reporterType);
        else
            _controller.DisableReporter(reporterType);

        // Act
        var currentVersion = _controller.GetCurrentVersion(); // Get the current version before calling ShouldTrack
        return _controller.ShouldTrack(currentVersion, reporterType, null, [reporterId]);
    }

    [Test]
    public void TemporarilyEnableReporter_EnablesReporterAndRevertOnDispose()
    {
        // Arrange
        var reporterType = typeof(TestWorkflowReporter);
        _controller.Enable(); // Monitoring must be enabled to enable reporters
        _controller.DisableReporter(reporterType);

        // Act & Assert
        Assert.That(_controller.IsReporterEnabled(reporterType), Is.False);
        using (var _ = _controller.TemporarilyEnableReporter<TestWorkflowReporter>())
        {
            Assert.That(_controller.IsReporterEnabled(reporterType), Is.True);
        }
        Assert.That(_controller.IsReporterEnabled(reporterType), Is.False);
    }

    [Test]
    public void GetCurrentVersion_ChangesAfterStateChange()
    {
        // Arrange
        var initialVersion = _controller.GetCurrentVersion();

        // Act
        _controller.EnableReporter(typeof(TestWorkflowReporter));
        var newVersion = _controller.GetCurrentVersion();

        // Assert
        Assert.That(newVersion, Is.Not.EqualTo(initialVersion));
    }

    [Test]
    public void GlobalDisableEnable_RestoresCorrectComponentStates()
    {
        // Arrange
        var reporterType = typeof(TestWorkflowReporter);
        var filterType = typeof(WorkflowItemFilter);
        _controller.EnableReporter(reporterType);
        _controller.EnableFilter(filterType);

        // Act
        _controller.Disable();
        _controller.Enable();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(_controller.IsReporterEnabled(reporterType), Is.True);
            Assert.That(_controller.IsFilterEnabled(filterType), Is.True);
        });
    }

    [Test]
    public void ConcurrentEnableDisable_MaintainsConsistentState()
    {
        // Arrange
        var reporterType = typeof(TestWorkflowReporter);
        var tasks = new List<Task>();

        // Act
        for (int i = 0; i < 100; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                _controller.EnableReporter(reporterType);
                _controller.DisableReporter(reporterType);
            }));
        }

        Task.WaitAll(tasks.ToArray());

        // Assert
        Assert.That(_controller.IsReporterEnabled(reporterType), Is.False);
    }

    [Test]
    public void VersionChanged_EventFired_WhenVersionChanges()
    {
        // Arrange
        var eventFired = false;
        MonitoringVersion? newVersion = null;

        _controller.VersionChanged += (_, args) =>
        {
            eventFired = true;
            newVersion = args.NewVersion;
        };

        // Act
        _controller.EnableReporter(typeof(TestWorkflowReporter));

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(eventFired, Is.True);
            Assert.That(newVersion, Is.Not.Null);
            Assert.That(newVersion, Is.EqualTo(_controller.GetCurrentVersion()));
        });
    }

    [Test]
    public void BeginOperation_ReturnsCorrectVersion()
    {
        // Act
        using (_controller.BeginOperation(out var operationVersion))
        {
            // Assert
            Assert.That(operationVersion, Is.EqualTo(_controller.GetCurrentVersion()));
        }
    }

    [Test]
    public void Configuration_WhenSet_TriggersVersionChange()
    {
        // Arrange
        var initialVersion = _controller.GetCurrentVersion();
        var newConfig = new MonitoringConfiguration();

        // Act
        _controller.Configuration = newConfig;

        // Assert
        Assert.That(_controller.GetCurrentVersion(), Is.Not.EqualTo(initialVersion));
    }

    [Test]
    public async Task LongRunningOperation_HandlesVersionChangesAsync()
    {
        // Arrange
        var initialVersion = _controller.GetCurrentVersion();
        var operationTask = Task.Run(async () =>
        {
            using (_controller.BeginOperation(out var operationVersion))
            {
                Assert.That(initialVersion, Is.EqualTo(operationVersion));
                await Task.Delay(1000); // Simulate long-running operation
                return _controller.ShouldTrack(operationVersion, typeof(TestWorkflowReporter));
            }
        });

        await Task.Delay(200); // Give some time for the operation to start
        _controller.EnableReporter(typeof(MockReporter)); // This should change the version

        // Act
        var result = await operationTask;

        // Assert
        Assert.That(result, Is.False, "Long-running operation should not track after version change");
    }
}

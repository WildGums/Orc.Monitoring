namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Reporters;
using Filters;


[TestFixture]
public class MonitoringControllerTests
{
    private TestLogger<MonitoringControllerTests> _logger;
    private TestLoggerFactory<MonitoringControllerTests> _loggerFactory;
    private MonitoringController _monitoringController;

    [SetUp]
    public void Setup()
    {
        _logger = new TestLogger<MonitoringControllerTests>();
        _loggerFactory = new TestLoggerFactory<MonitoringControllerTests>(_logger);

        _monitoringController = new MonitoringController(_loggerFactory, () => new EnhancedDataPostProcessor(_loggerFactory));

        _monitoringController.Enable(); // Enable monitoring by default for tests
    }

    private void EnableReporter(Type reporterType)
    {
        _monitoringController.EnableReporter(reporterType);
        Assert.That(_monitoringController.IsReporterEnabled(reporterType), Is.True);
    }

    private void DisableReporter(Type reporterType)
    {
        _monitoringController.DisableReporter(reporterType);
        Assert.That(_monitoringController.IsReporterEnabled(reporterType), Is.False);
    }

    private void EnableFilter(Type filterType)
    {
        _monitoringController.EnableFilter(filterType);
        Assert.That(_monitoringController.IsFilterEnabled(filterType), Is.True);
    }

    private void DisableFilter(Type filterType)
    {
        _monitoringController.DisableFilter(filterType);
        Assert.That(_monitoringController.IsFilterEnabled(filterType), Is.False);
    }

    [Test]
    public void EnableFilterForReporterType_EnablesFilterForSpecificReporter()
    {
        var reporterType = typeof(TestWorkflowReporter);
        var filterType = typeof(WorkflowItemFilter);

        _monitoringController.EnableReporter(reporterType);
        _monitoringController.EnableFilterForReporterType(reporterType, filterType);

        Assert.That(_monitoringController.IsFilterEnabledForReporterType(reporterType, filterType), Is.True);
    }


    [Test]
    public void EnableReporter_WhenCalled_EnablesReporter()
    {
        EnableReporter(typeof(TestWorkflowReporter));
    }

    [Test]
    public void DisableReporter_WhenCalled_DisablesReporter()
    {
        EnableReporter(typeof(TestWorkflowReporter));
        DisableReporter(typeof(TestWorkflowReporter));
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

    [TestCase(true, true, ExpectedResult = true)]
    [TestCase(false, true, ExpectedResult = false)]
    public bool ShouldTrack_ReturnsExpectedResult(bool monitoringEnabled, bool reporterEnabled)
    {
        var builder = new ConfigurationBuilder(_monitoringController);
        builder.SetGlobalState(monitoringEnabled);

        var reporter = new MockReporter(_loggerFactory); // Use MockReporter instead of TestWorkflowReporter
        if (reporterEnabled)
            builder.AddReporterType(typeof(TestWorkflowReporter));

        _monitoringController.Configuration = builder.Build();

        var currentVersion = _monitoringController.GetCurrentVersion();
        return _monitoringController.ShouldTrack(currentVersion, reporterIds: new[] { reporter.Id });
    }

    [Test]
    public void TemporarilyEnableReporter_EnablesReporterAndRevertOnDispose()
    {
        _monitoringController.DisableReporter(typeof(TestWorkflowReporter));
        Assert.That(_monitoringController.IsReporterEnabled(typeof(TestWorkflowReporter)), Is.False);

        using (var temp = _monitoringController.TemporarilyEnableReporter<TestWorkflowReporter>())
        {
            Assert.That(_monitoringController.IsReporterEnabled(typeof(TestWorkflowReporter)), Is.True);
        }

        Assert.That(_monitoringController.IsReporterEnabled(typeof(TestWorkflowReporter)), Is.False);
    }

    [Test]
    public void TemporarilyEnableFilter_EnablesFilterAndRevertOnDispose()
    {
        _monitoringController.DisableFilter(typeof(WorkflowItemFilter));
        Assert.That(_monitoringController.IsFilterEnabled(typeof(WorkflowItemFilter)), Is.False);

        using (var temp = _monitoringController.TemporarilyEnableFilter<WorkflowItemFilter>())
        {
            Assert.That(_monitoringController.IsFilterEnabled(typeof(WorkflowItemFilter)), Is.True);
        }

        Assert.That(_monitoringController.IsFilterEnabled(typeof(WorkflowItemFilter)), Is.False);
    }

    [Test]
    public void GetCurrentVersion_ChangesAndIncreasesAfterStateChange()
    {
        var initialVersion = _monitoringController.GetCurrentVersion();
        Console.WriteLine($"Initial version: {initialVersion}");

        Thread.Sleep(10); // Ensure timestamp change

        _monitoringController.EnableReporter(typeof(TestWorkflowReporter));
        var newVersion = _monitoringController.GetCurrentVersion();
        Console.WriteLine($"New version: {newVersion}");

        Assert.Multiple(() =>
        {
            Assert.That(newVersion, Is.GreaterThan(initialVersion), "Version should increase after state change");
            Assert.That(newVersion, Is.Not.EqualTo(initialVersion), "Version should change after state change");
        });

        var versionHistory = MonitoringDiagnostics.GetVersionHistory();
        Console.WriteLine("Version History:");
        foreach (var change in versionHistory)
        {
            Console.WriteLine($"  {change.Timestamp}: {change.OldVersion} -> {change.NewVersion}");
        }
    }

    [Test]
    public void GlobalDisableEnable_RestoresCorrectComponentStates()
    {
        var builder = new ConfigurationBuilder(_monitoringController);
        builder.AddReporterType(typeof(TestWorkflowReporter));
        builder.AddFilter(new AlwaysIncludeFilter(_loggerFactory));
        _monitoringController.Configuration = builder.Build();

        _monitoringController.Disable();
        _monitoringController.Enable();

        Assert.Multiple(() =>
        {
            Assert.That(_monitoringController.IsReporterEnabled(typeof(TestWorkflowReporter)), Is.True);
            Assert.That(_monitoringController.IsFilterEnabled(typeof(AlwaysIncludeFilter)), Is.True);
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
                _monitoringController.EnableReporter(typeof(TestWorkflowReporter));
                _monitoringController.DisableReporter(typeof(TestWorkflowReporter));
            }));
        }

        Task.WaitAll(tasks.ToArray());

        Assert.That(_monitoringController.IsReporterEnabled(typeof(TestWorkflowReporter)), Is.False);
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

        _monitoringController.EnableReporter(typeof(TestWorkflowReporter));

        Assert.That(eventFired, Is.True);
        Assert.That(newVersion, Is.Not.Null);
        Assert.That(newVersion, Is.EqualTo(_monitoringController.GetCurrentVersion()));
    }

    [Test]
    public void BeginOperation_ReturnsCorrectVersion()
    {
        using (_monitoringController.BeginOperation(out var operationVersion))
        {
            Assert.That(operationVersion, Is.EqualTo(_monitoringController.GetCurrentVersion()));
        }
    }

    [Test]
    public void Configuration_WhenSet_TriggersVersionChange()
    {
        _monitoringController.Enable(); // Ensure monitoring is enabled
        var initialVersion = _monitoringController.GetCurrentVersion();
        Console.WriteLine($"Initial version: {initialVersion}");

        var builder = new ConfigurationBuilder(_monitoringController);
        builder.AddReporterType(typeof(TestWorkflowReporter));
        _monitoringController.Configuration = builder.Build();

        var newVersion = _monitoringController.GetCurrentVersion();
        Console.WriteLine($"New version after setting Configuration: {newVersion}");

        Assert.That(newVersion, Is.GreaterThan(initialVersion), "Version should increase after setting Configuration");

        var versionHistory = MonitoringDiagnostics.GetVersionHistory();
        Console.WriteLine("Version History:");
        foreach (var change in versionHistory)
        {
            Console.WriteLine($"  {change.Timestamp}: {change.OldVersion} -> {change.NewVersion}");
        }

        Console.WriteLine($"Is Monitoring Enabled: {_monitoringController.IsEnabled}");
    }

    [Test]
    public void VersionChange_UpdatesCacheAndNotifiesListeners()
    {
        var versionChangeCount = 0;
        _monitoringController.VersionChanged += (sender, version) => versionChangeCount++;

        var initialVersion = _monitoringController.GetCurrentVersion();
        var reporterType = typeof(TestWorkflowReporter);

        _monitoringController.EnableReporter(reporterType);
        var afterFirstEnableVersion = _monitoringController.GetCurrentVersion();

        Assert.That(afterFirstEnableVersion, Is.Not.EqualTo(initialVersion));
        Assert.That(_monitoringController.ShouldTrack(afterFirstEnableVersion, reporterType), Is.True);

        _monitoringController.EnableReporter(typeof(MockReporter));
        var finalVersion = _monitoringController.GetCurrentVersion();

        Assert.Multiple(() =>
        {
            Assert.That(finalVersion, Is.Not.EqualTo(afterFirstEnableVersion));
            Assert.That(_monitoringController.ShouldTrack(initialVersion, reporterType), Is.False);
            Assert.That(_monitoringController.ShouldTrack(afterFirstEnableVersion, reporterType), Is.False);
            Assert.That(_monitoringController.ShouldTrack(finalVersion, reporterType), Is.True);
            Assert.That(versionChangeCount, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task LongRunningOperation_HandlesVersionChangesAsync()
    {
        var initialVersion = _monitoringController.GetCurrentVersion();
        var operationTask = Task.Run(async () =>
        {
            using (_monitoringController.BeginOperation(out var operationVersion))
            {
                Assert.That(initialVersion, Is.EqualTo(operationVersion));
                await Task.Delay(1000); // Simulate long-running operation
                return _monitoringController.ShouldTrack(operationVersion, typeof(TestWorkflowReporter));
            }
        });

        await Task.Delay(200); // Give some time for the operation to start
        _monitoringController.EnableReporter(typeof(MockReporter)); // This should change the version

        var result = await operationTask;
        Assert.That(result, Is.False,  "Long-running operation should not track after version change");
    }

    [Test]
    public void VersionOverflow_HandledGracefully()
    {
        // Set the version to the maximum value
        typeof(MonitoringController).GetField("_currentVersion", BindingFlags.NonPublic | BindingFlags.Static)
            ?.SetValue(null, new MonitoringVersion(long.MaxValue, int.MaxValue, Guid.NewGuid()));

        var beforeOverflow = _monitoringController.GetCurrentVersion();
        _monitoringController.EnableReporter(typeof(TestWorkflowReporter)); // This should increment the version
        var afterOverflow = _monitoringController.GetCurrentVersion();

        Assert.That(afterOverflow.Timestamp, Is.LessThan(beforeOverflow.Timestamp));
        Assert.That(afterOverflow, Is.LessThan(beforeOverflow));
    }

    [Test]
    public void VersionChange_LogsChangeAndGeneratesReport()
    {
        var initialVersion = _monitoringController.GetCurrentVersion();
        _monitoringController.EnableReporter(typeof(TestWorkflowReporter));
        var newVersion = _monitoringController.GetCurrentVersion();

        Assert.That(newVersion, Is.Not.EqualTo(initialVersion));

        var report = _monitoringController.GenerateVersionReport();
        Assert.That(report, Does.Contain(initialVersion.ToString()));
        Assert.That(report, Does.Contain(newVersion.ToString()));
    }
}

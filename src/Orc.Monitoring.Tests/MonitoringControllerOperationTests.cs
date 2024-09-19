#pragma warning disable CL0002
namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TestUtilities.Logging;
using TestUtilities.Mocks;

[TestFixture]
public class MonitoringControllerOperationTests
{
    private TestLogger<MonitoringControllerOperationTests> _logger;
    private TestLoggerFactory<MonitoringControllerOperationTests> _loggerFactory;
    private MonitoringController _monitoringController;

    [SetUp]
    public void Setup()
    {
        _logger = new TestLogger<MonitoringControllerOperationTests>();
        _loggerFactory = new TestLoggerFactory<MonitoringControllerOperationTests>(_logger);

        _monitoringController = new MonitoringController(_loggerFactory);

        _monitoringController.Enable();
    }

    [Test]
    public void BeginOperation_ReturnsCurrentVersion()
    {
        var currentVersion = _monitoringController.GetCurrentVersion();

        using (_monitoringController.BeginOperation(out var operationVersion))
        {
            Assert.That(operationVersion, Is.EqualTo(currentVersion));
        }
    }

    [Test]
    public void BeginOperation_NestedOperations_WorkCorrectly()
    {
        using (_monitoringController.BeginOperation(out var outerVersion))
        {
            Assert.That(_monitoringController.IsOperationValid(), Is.True);

            using (_monitoringController.BeginOperation(out var innerVersion))
            {
                Assert.That(_monitoringController.IsOperationValid(), Is.True);
                Assert.That(innerVersion, Is.EqualTo(outerVersion));
            }

            Assert.That(_monitoringController.IsOperationValid(), Is.True);
        }

        Assert.That(_monitoringController.IsOperationValid(), Is.False);
    }

    [Test]
    public void IsOperationValid_OutsideOperation_ReturnsFalse()
    {
        Assert.That(_monitoringController.IsOperationValid(), Is.False);
    }

    [Test]
    public void IsOperationValid_DuringOperation_ReturnsTrue()
    {
        using (_monitoringController.BeginOperation(out _))
        {
            Assert.That(_monitoringController.IsOperationValid(), Is.True);
        }
    }

    [Test]
    public void IsOperationValid_AfterVersionChange_ReturnsFalse()
    {
        using (_monitoringController.BeginOperation(out _))
        {
            Assert.That(_monitoringController.IsOperationValid(), Is.True);

            _monitoringController.EnableReporter(typeof(MockReporter)); // This will update the version

            Assert.That(_monitoringController.IsOperationValid(), Is.False);
        }
    }

    [Test]
    public async Task BeginOperation_AsyncOperation_MaintainsContext()
    {
        using (_monitoringController.BeginOperation(out var operationVersion))
        {
            Assert.That(_monitoringController.IsOperationValid(), Is.True);

            await Task.Yield(); // Switch to a different context

            Assert.That(_monitoringController.IsOperationValid(), Is.True);
            Assert.That(_monitoringController.ShouldTrack(operationVersion), Is.True);
        }

        Assert.That(_monitoringController.IsOperationValid(), Is.False);
    }

    [Test]
    public void ShouldTrack_DuringOperation_UsesOperationVersion()
    {
        _monitoringController.Enable();
        _logger.LogInformation($"Test start - IsEnabled: {_monitoringController.IsEnabled}");

        var initialVersion = _monitoringController.GetCurrentVersion();
        _logger.LogInformation($"Initial version: {initialVersion}");

        using (_monitoringController.BeginOperation(out var operationVersion))
        {
            _logger.LogInformation($"Operation version: {operationVersion}");
            Assert.That(operationVersion, Is.EqualTo(initialVersion), "Operation version should initially match the current version");

            // ShouldTrack should return true for the operation version initially
            Assert.That(_monitoringController.ShouldTrack(operationVersion), Is.True, "Should track for operation version initially");

            Thread.Sleep(10); // Ensure a new timestamp for the next version

            _logger.LogInformation($"Before enabling reporter - IsEnabled: {_monitoringController.IsEnabled}, MockReporter enabled: {_monitoringController.IsReporterEnabled(typeof(MockReporter))}");
            _monitoringController.EnableReporter(typeof(MockReporter)); // This should update the version
            _logger.LogInformation($"After enabling reporter - MockReporter enabled: {_monitoringController.IsReporterEnabled(typeof(MockReporter))}");

            var currentVersion = _monitoringController.GetCurrentVersion();
            _logger.LogInformation($"Current version after EnableReporter: {currentVersion}");

            Assert.That(currentVersion, Is.GreaterThan(initialVersion), "Version should increment after enabling reporter");

            // ShouldTrack should return false for the operation version after global version change
            Assert.That(_monitoringController.ShouldTrack(operationVersion), Is.False, "Should not track for operation version after global version change");

            // ShouldTrack should return false for the initial version
            Assert.That(_monitoringController.ShouldTrack(initialVersion), Is.False, "Should not track for initial version");

            // ShouldTrack should return true for the current version (which is newer than the operation version)
            Assert.That(_monitoringController.ShouldTrack(currentVersion), Is.True, "Should track for current version");
        }

        // Outside the operation, ShouldTrack should now return false for the initial version
        Assert.That(_monitoringController.ShouldTrack(initialVersion), Is.False, "Should not track for initial version outside operation");

        // Outside the operation, ShouldTrack should return true for the current version
        Assert.That(_monitoringController.ShouldTrack(_monitoringController.GetCurrentVersion()), Is.True, "Should track for current version outside operation");

        _logger.LogInformation("Printing version history:");
        var versionHistory = MonitoringDiagnostics.GetVersionHistory();
        foreach (var change in versionHistory)
        {
            _logger.LogInformation($"  {change.Timestamp}: {change.OldVersion} -> {change.NewVersion}");
        }
    }

    [Test]
    public async Task BeginOperation_LongRunningOperation_HandlesVersionChangesCorrectly()
    {
        var initialVersion = _monitoringController.GetCurrentVersion();

        var operationTask = Task.Run(async () =>
        {
            using (_monitoringController.BeginOperation(out var operationVersion))
            {
                Assert.That(operationVersion, Is.EqualTo(initialVersion));
                await Task.Delay(1000); // Simulate long-running operation
                return _monitoringController.ShouldTrack(operationVersion);
            }
        });

        await Task.Delay(100); // Give some time for the operation to start

        _monitoringController.EnableReporter(typeof(MockReporter)); // This will update the version

        var result = await operationTask;

        Assert.That(result, Is.False, "ShouldTrack should return false for the operation version after global version change");
        Assert.That(_monitoringController.ShouldTrack(initialVersion), Is.False, "ShouldTrack should return false for the initial version outside the operation");
        Assert.That(_monitoringController.ShouldTrack(_monitoringController.GetCurrentVersion()), Is.True, "ShouldTrack should return true for the current version");
    }
}

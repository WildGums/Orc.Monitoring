namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;
using Reporters;

[TestFixture]
public class MonitoringControllerShouldTrackTests
{
    private MockReporter _mockReporter;
    private TestLogger<MonitoringControllerShouldTrackTests> _logger;
    private TestLoggerFactory<MonitoringControllerShouldTrackTests> _loggerFactory;
    private MonitoringController _monitoringController;

    [SetUp]
    public void Setup()
    {
        _logger = new TestLogger<MonitoringControllerShouldTrackTests>();
        _loggerFactory = new TestLoggerFactory<MonitoringControllerShouldTrackTests>(_logger);

        _monitoringController = new MonitoringController(_loggerFactory, () => new EnhancedDataPostProcessor(_loggerFactory));  

        _monitoringController.Enable();
        
        _mockReporter = new MockReporter(_loggerFactory);
    }

    [Test]
    public void ShouldTrack_WhenEnabled_ReturnsTrue()
    {
        var version = _monitoringController.GetCurrentVersion();
        Assert.That(_monitoringController.ShouldTrack(version), Is.True);
    }

    [Test]
    public void ShouldTrack_WhenDisabled_ReturnsFalse()
    {
        var version = _monitoringController.GetCurrentVersion();
        _monitoringController.Disable();
        Assert.That(_monitoringController.ShouldTrack(version), Is.False);
    }

    [Test]
    public void ShouldTrack_WithOlderVersion_ReturnsExpectedResult()
    {
        _monitoringController.Enable();
        var oldVersion = _monitoringController.GetCurrentVersion();
        Console.WriteLine($"Old version: {oldVersion}");

        // Add a small delay to ensure version change
        Thread.Sleep(10);

        _monitoringController.EnableReporter(typeof(MockReporter)); // This will update the version
        var newVersion = _monitoringController.GetCurrentVersion();
        Console.WriteLine($"New version: {newVersion}");

        Assert.That(oldVersion, Is.LessThan(newVersion), "New version should be greater than old version");
        Assert.That(_monitoringController.ShouldTrack(oldVersion), Is.False, "Should not track older version");
        Assert.That(_monitoringController.ShouldTrack(newVersion), Is.True, "Should track current version");
    }

    [Test]
    public void ShouldTrack_WithReporterIds_ReturnsExpectedResult()
    {
        _monitoringController.Enable();
        var reporter1 = new MockReporter(_loggerFactory) { Id = "Reporter1" };
        var reporter2 = new MockReporter(_loggerFactory) { Id = "Reporter2" };
        _monitoringController.EnableReporter(typeof(MockReporter));

        var version = _monitoringController.GetCurrentVersion();

        Assert.That(_monitoringController.ShouldTrack(version, reporterIds: new[] { "Reporter1" }), Is.True);
        Assert.That(_monitoringController.ShouldTrack(version, reporterIds: new[] { "Reporter2" }), Is.True);
        Assert.That(_monitoringController.ShouldTrack(version, reporterIds: new[] { "Reporter3" }), Is.True);

        _monitoringController.DisableReporter(typeof(MockReporter));
        Assert.That(_monitoringController.ShouldTrack(version, reporterIds: new[] { "Reporter1" }), Is.False);
    }

    [Test]
    public void ShouldTrack_WithNewerVersion_ReturnsFalse()
    {
        var currentVersion = _monitoringController.GetCurrentVersion();
        var newerVersion = new MonitoringVersion(currentVersion.Timestamp + 1, 0, Guid.NewGuid());
        Assert.That(_monitoringController.ShouldTrack(newerVersion), Is.False);
    }

    [Test]
    public void ShouldTrack_WithEnabledReporter_ReturnsTrue()
    {
        _monitoringController.EnableReporter(typeof(MockReporter));
        var version = _monitoringController.GetCurrentVersion();
        Assert.That(_monitoringController.ShouldTrack(version, typeof(MockReporter)), Is.True);
    }

    [Test]
    public void ShouldTrack_WithDisabledReporter_ReturnsFalse()
    {
        _monitoringController.DisableReporter(typeof(MockReporter));
        var version = _monitoringController.GetCurrentVersion();
        Assert.That(_monitoringController.ShouldTrack(version, typeof(MockReporter)), Is.False);
    }

    [Test]
    public void ShouldTrack_CachesResults()
    {
        var version = _monitoringController.GetCurrentVersion();
        var result1 = _monitoringController.ShouldTrack(version);
        var result2 = _monitoringController.ShouldTrack(version);
        Assert.That(result1, Is.EqualTo(result2));
    }

    [Test]
    public void ShouldTrack_InvalidatesCacheOnVersionChange()
    {
        _monitoringController.Enable(); // Ensure monitoring is enabled
        var version = _monitoringController.GetCurrentVersion();
        Console.WriteLine($"Initial version: {version}");

        var result1 = _monitoringController.ShouldTrack(version);
        Console.WriteLine($"First ShouldTrack result: {result1}");

        Assert.That(result1, Is.True, "Initial ShouldTrack should return true");

        _monitoringController.EnableReporter(typeof(MockReporter)); // This should update the version
        var newVersion = _monitoringController.GetCurrentVersion();
        Console.WriteLine($"New version after enabling reporter: {newVersion}");

        var result2 = _monitoringController.ShouldTrack(version);
        Console.WriteLine($"Second ShouldTrack result (with old version): {result2}");

        Assert.That(result2, Is.False, "ShouldTrack should return false for the old version after a version change");

        var result3 = _monitoringController.ShouldTrack(newVersion);
        Console.WriteLine($"Third ShouldTrack result (with new version): {result3}");

        Assert.That(result3, Is.True, "ShouldTrack should return true for the new version");

        var versionHistory = MonitoringDiagnostics.GetVersionHistory();
        Console.WriteLine("Version History:");
        foreach (var change in versionHistory)
        {
            Console.WriteLine($"  {change.Timestamp}: {change.OldVersion} -> {change.NewVersion}");
        }

        Console.WriteLine($"Is Monitoring Enabled: {_monitoringController.IsEnabled}");
        Console.WriteLine($"Is MockReporter Enabled: {_monitoringController.IsReporterEnabled(typeof(MockReporter))}");
    }

    [Test]
    public void ShouldTrack_ConcurrentAccess()
    {
        const int taskCount = 1000;
        var tasks = new Task[taskCount];
        var results = new bool[taskCount];

        for (int i = 0; i < taskCount; i++)
        {
            var index = i;
            tasks[i] = Task.Run(() =>
            {
                var version = _monitoringController.GetCurrentVersion();
                results[index] = _monitoringController.ShouldTrack(version);
            });
        }

        Task.WaitAll(tasks);

        Assert.That(results, Is.All.True);
    }
}

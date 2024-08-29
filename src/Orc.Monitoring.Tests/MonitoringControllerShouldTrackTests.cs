namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;


[TestFixture]
public class MonitoringControllerShouldTrackTests
{
    private MockReporter _mockReporter;
    private TestLogger<MonitoringControllerShouldTrackTests> _logger;

    [SetUp]
    public void Setup()
    {
        _logger = new TestLogger<MonitoringControllerShouldTrackTests>();

        MonitoringController.ResetForTesting();
        MonitoringController.Enable();
        
        _mockReporter = new MockReporter(_logger.CreateLogger<MockReporter>());
    }

    [Test]
    public void ShouldTrack_WhenEnabled_ReturnsTrue()
    {
        var version = MonitoringController.GetCurrentVersion();
        Assert.That(MonitoringController.ShouldTrack(version), Is.True);
    }

    [Test]
    public void ShouldTrack_WhenDisabled_ReturnsFalse()
    {
        var version = MonitoringController.GetCurrentVersion();
        MonitoringController.Disable();
        Assert.That(MonitoringController.ShouldTrack(version), Is.False);
    }

    [Test]
    public void ShouldTrack_WithOlderVersion_ReturnsExpectedResult()
    {
        MonitoringController.Enable();
        var oldVersion = MonitoringController.GetCurrentVersion();
        Console.WriteLine($"Old version: {oldVersion}");

        // Add a small delay to ensure version change
        Thread.Sleep(10);

        MonitoringController.EnableReporter(typeof(MockReporter)); // This will update the version
        var newVersion = MonitoringController.GetCurrentVersion();
        Console.WriteLine($"New version: {newVersion}");

        Assert.That(oldVersion, Is.LessThan(newVersion), "New version should be greater than old version");
        Assert.That(MonitoringController.ShouldTrack(oldVersion), Is.False, "Should not track older version");
        Assert.That(MonitoringController.ShouldTrack(newVersion), Is.True, "Should track current version");
    }

    [Test]
    public void ShouldTrack_WithReporterIds_ReturnsExpectedResult()
    {
        MonitoringController.Enable();
        var reporter1 = new MockReporter(_logger.CreateLogger<MockReporter>()) { Id = "Reporter1" };
        var reporter2 = new MockReporter(_logger.CreateLogger<MockReporter>()) { Id = "Reporter2" };
        MonitoringController.EnableReporter(typeof(MockReporter));

        var version = MonitoringController.GetCurrentVersion();

        Assert.That(MonitoringController.ShouldTrack(version, reporterIds: new[] { "Reporter1" }), Is.True);
        Assert.That(MonitoringController.ShouldTrack(version, reporterIds: new[] { "Reporter2" }), Is.True);
        Assert.That(MonitoringController.ShouldTrack(version, reporterIds: new[] { "Reporter3" }), Is.True);

        MonitoringController.DisableReporter(typeof(MockReporter));
        Assert.That(MonitoringController.ShouldTrack(version, reporterIds: new[] { "Reporter1" }), Is.False);
    }

    [Test]
    public void ShouldTrack_WithNewerVersion_ReturnsFalse()
    {
        var currentVersion = MonitoringController.GetCurrentVersion();
        var newerVersion = new MonitoringVersion(currentVersion.Timestamp + 1, 0, Guid.NewGuid());
        Assert.That(MonitoringController.ShouldTrack(newerVersion), Is.False);
    }

    [Test]
    public void ShouldTrack_WithEnabledReporter_ReturnsTrue()
    {
        MonitoringController.EnableReporter(typeof(MockReporter));
        var version = MonitoringController.GetCurrentVersion();
        Assert.That(MonitoringController.ShouldTrack(version, typeof(MockReporter)), Is.True);
    }

    [Test]
    public void ShouldTrack_WithDisabledReporter_ReturnsFalse()
    {
        MonitoringController.DisableReporter(typeof(MockReporter));
        var version = MonitoringController.GetCurrentVersion();
        Assert.That(MonitoringController.ShouldTrack(version, typeof(MockReporter)), Is.False);
    }

    [Test]
    public void ShouldTrack_CachesResults()
    {
        var version = MonitoringController.GetCurrentVersion();
        var result1 = MonitoringController.ShouldTrack(version);
        var result2 = MonitoringController.ShouldTrack(version);
        Assert.That(result1, Is.EqualTo(result2));
    }

    [Test]
    public void ShouldTrack_InvalidatesCacheOnVersionChange()
    {
        MonitoringController.Enable(); // Ensure monitoring is enabled
        var version = MonitoringController.GetCurrentVersion();
        Console.WriteLine($"Initial version: {version}");

        var result1 = MonitoringController.ShouldTrack(version);
        Console.WriteLine($"First ShouldTrack result: {result1}");

        Assert.That(result1, Is.True, "Initial ShouldTrack should return true");

        MonitoringController.EnableReporter(typeof(MockReporter)); // This should update the version
        var newVersion = MonitoringController.GetCurrentVersion();
        Console.WriteLine($"New version after enabling reporter: {newVersion}");

        var result2 = MonitoringController.ShouldTrack(version);
        Console.WriteLine($"Second ShouldTrack result (with old version): {result2}");

        Assert.That(result2, Is.False, "ShouldTrack should return false for the old version after a version change");

        var result3 = MonitoringController.ShouldTrack(newVersion);
        Console.WriteLine($"Third ShouldTrack result (with new version): {result3}");

        Assert.That(result3, Is.True, "ShouldTrack should return true for the new version");

        var versionHistory = MonitoringDiagnostics.GetVersionHistory();
        Console.WriteLine("Version History:");
        foreach (var change in versionHistory)
        {
            Console.WriteLine($"  {change.Timestamp}: {change.OldVersion} -> {change.NewVersion}");
        }

        Console.WriteLine($"Is Monitoring Enabled: {MonitoringController.IsEnabled}");
        Console.WriteLine($"Is MockReporter Enabled: {MonitoringController.IsReporterEnabled(typeof(MockReporter))}");
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
                var version = MonitoringController.GetCurrentVersion();
                results[index] = MonitoringController.ShouldTrack(version);
            });
        }

        Task.WaitAll(tasks);

        Assert.That(results, Is.All.True);
    }
}

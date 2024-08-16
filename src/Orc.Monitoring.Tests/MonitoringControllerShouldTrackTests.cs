namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using Orc.Monitoring.Reporters.ReportOutputs;
using Orc.Monitoring.Reporters;
using System;
using System.Threading.Tasks;


[TestFixture]
public class MonitoringControllerShouldTrackTests
{
    [SetUp]
    public void Setup()
    {
        MonitoringController.ResetForTesting();
        MonitoringController.Enable();
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
    public void ShouldTrack_WithOlderVersion_ReturnsTrue()
    {
        var oldVersion = MonitoringController.GetCurrentVersion();
        MonitoringController.EnableReporter(typeof(DummyReporter)); // This will update the version
        Assert.That(MonitoringController.ShouldTrack(oldVersion, allowOlderVersions: true), Is.True);
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
        MonitoringController.EnableReporter(typeof(DummyReporter));
        var version = MonitoringController.GetCurrentVersion();
        Assert.That(MonitoringController.ShouldTrack(version, typeof(DummyReporter)), Is.True);
    }

    [Test]
    public void ShouldTrack_WithDisabledReporter_ReturnsFalse()
    {
        MonitoringController.DisableReporter(typeof(DummyReporter));
        var version = MonitoringController.GetCurrentVersion();
        Assert.That(MonitoringController.ShouldTrack(version, typeof(DummyReporter)), Is.False);
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

        MonitoringController.EnableReporter(typeof(DummyReporter)); // This should update the version
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
        Console.WriteLine($"Is DummyReporter Enabled: {MonitoringController.IsReporterEnabled(typeof(DummyReporter))}");
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

    private class DummyReporter : IMethodCallReporter
    {
        public string Name => "DummyReporter";
        public string FullName => "DummyReporter";
        public System.Reflection.MethodInfo? RootMethod { get; set; }

        public IAsyncDisposable StartReporting(IObservable<MethodLifeCycleItems.ICallStackItem> callStack)
        {
            throw new NotImplementedException();
        }

        public IOutputContainer AddOutput<TOutput>(object? parameter = null) where TOutput : IReportOutput, new()
        {
            throw new NotImplementedException();
        }
    }
}

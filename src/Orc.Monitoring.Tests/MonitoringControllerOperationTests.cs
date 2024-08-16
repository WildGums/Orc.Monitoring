#pragma warning disable CL0002
namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using Orc.Monitoring.Reporters;
using Orc.Monitoring.Reporters.ReportOutputs;
using System;
using System.Threading.Tasks;


[TestFixture]
public class MonitoringControllerOperationTests
{
    [SetUp]
    public void Setup()
    {
        MonitoringController.ResetForTesting();
        MonitoringController.Enable();
    }

    [Test]
    public void BeginOperation_ReturnsCurrentVersion()
    {
        var currentVersion = MonitoringController.GetCurrentVersion();

        using (MonitoringController.BeginOperation(out var operationVersion))
        {
            Assert.That(operationVersion, Is.EqualTo(currentVersion));
        }
    }

    [Test]
    public void BeginOperation_NestedOperations_WorkCorrectly()
    {
        using (MonitoringController.BeginOperation(out var outerVersion))
        {
            Assert.That(MonitoringController.IsOperationValid(), Is.True);

            using (MonitoringController.BeginOperation(out var innerVersion))
            {
                Assert.That(MonitoringController.IsOperationValid(), Is.True);
                Assert.That(innerVersion, Is.EqualTo(outerVersion));
            }

            Assert.That(MonitoringController.IsOperationValid(), Is.True);
        }

        Assert.That(MonitoringController.IsOperationValid(), Is.False);
    }

    [Test]
    public void IsOperationValid_OutsideOperation_ReturnsFalse()
    {
        Assert.That(MonitoringController.IsOperationValid(), Is.False);
    }

    [Test]
    public void IsOperationValid_DuringOperation_ReturnsTrue()
    {
        using (MonitoringController.BeginOperation(out _))
        {
            Assert.That(MonitoringController.IsOperationValid(), Is.True);
        }
    }

    [Test]
    public void IsOperationValid_AfterVersionChange_ReturnsFalse()
    {
        using (MonitoringController.BeginOperation(out _))
        {
            Assert.That(MonitoringController.IsOperationValid(), Is.True);

            MonitoringController.EnableReporter(typeof(DummyReporter)); // This will update the version

            Assert.That(MonitoringController.IsOperationValid(), Is.False);
        }
    }

    [Test]
    public async Task BeginOperation_AsyncOperation_MaintainsContext()
    {
        using (MonitoringController.BeginOperation(out var operationVersion))
        {
            Assert.That(MonitoringController.IsOperationValid(), Is.True);

            await Task.Yield(); // Switch to a different context

            Assert.That(MonitoringController.IsOperationValid(), Is.True);
            Assert.That(MonitoringController.ShouldTrack(operationVersion), Is.True);
        }

        Assert.That(MonitoringController.IsOperationValid(), Is.False);
    }

    [Test]
    public void ShouldTrack_DuringOperation_UsesOperationVersion()
    {
        var initialVersion = MonitoringController.GetCurrentVersion();

        using (MonitoringController.BeginOperation(out var operationVersion))
        {
            Assert.That(operationVersion, Is.EqualTo(initialVersion));

            // ShouldTrack should return true for the operation version initially
            Assert.That(MonitoringController.ShouldTrack(operationVersion), Is.True, "Should track for operation version initially");

            MonitoringController.EnableReporter(typeof(DummyReporter)); // This will update the version

            var currentVersion = MonitoringController.GetCurrentVersion();
            Assert.That(currentVersion, Is.GreaterThan(initialVersion));

            // ShouldTrack should return false for the operation version after global version change
            Assert.That(MonitoringController.ShouldTrack(operationVersion), Is.False, "Should not track for operation version after global version change");

            // ShouldTrack should return false for the initial version
            Assert.That(MonitoringController.ShouldTrack(initialVersion), Is.False, "Should not track for initial version");

            // ShouldTrack should return true for the current version (which is newer than the operation version)
            Assert.That(MonitoringController.ShouldTrack(currentVersion), Is.True, "Should track for current version");
        }

        // Outside the operation, ShouldTrack should now return false for the initial version
        Assert.That(MonitoringController.ShouldTrack(initialVersion), Is.False, "Should not track for initial version outside operation");

        // Outside the operation, ShouldTrack should return true for the current version
        Assert.That(MonitoringController.ShouldTrack(MonitoringController.GetCurrentVersion()), Is.True, "Should track for current version outside operation");
    }

    [Test]
    public async Task BeginOperation_LongRunningOperation_HandlesVersionChangesCorrectly()
    {
        var initialVersion = MonitoringController.GetCurrentVersion();

        var operationTask = Task.Run(async () =>
        {
            using (MonitoringController.BeginOperation(out var operationVersion))
            {
                Assert.That(operationVersion, Is.EqualTo(initialVersion));
                await Task.Delay(1000); // Simulate long-running operation
                return MonitoringController.ShouldTrack(operationVersion);
            }
        });

        await Task.Delay(100); // Give some time for the operation to start

        MonitoringController.EnableReporter(typeof(DummyReporter)); // This will update the version

        var result = await operationTask;

        Assert.That(result, Is.False, "ShouldTrack should return false for the operation version after global version change");
        Assert.That(MonitoringController.ShouldTrack(initialVersion), Is.False, "ShouldTrack should return false for the initial version outside the operation");
        Assert.That(MonitoringController.ShouldTrack(MonitoringController.GetCurrentVersion()), Is.True, "ShouldTrack should return true for the current version");
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

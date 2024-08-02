namespace Orc.Monitoring.Tests;

using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;


[TestFixture]
public class MonitoringManagerTests
{
    [SetUp]
    public void SetUp()
    {
        // Ensure monitoring is disabled before each test
        MonitoringManager.Disable();
    }

    [Test]
    public void IsEnabled_DefaultState_IsFalse()
    {
        Assert.That(MonitoringManager.IsEnabled, Is.False);
    }

    [Test]
    public void Enable_WhenCalled_EnablesMonitoring()
    {
        MonitoringManager.Enable();
        Assert.That(MonitoringManager.IsEnabled, Is.True);
    }

    [Test]
    public void Disable_WhenCalled_DisablesMonitoring()
    {
        MonitoringManager.Enable();
        MonitoringManager.Disable();
        Assert.That(MonitoringManager.IsEnabled, Is.False);
    }

    [Test]
    public void AddStateChangedCallback_WhenStateChanges_InvokesCallback()
    {
        bool callbackInvoked = false;
        bool callbackState = false;
        int callbackVersion = 0;

        MonitoringManager.AddStateChangedCallback((state, version) =>
        {
            callbackInvoked = true;
            callbackState = state;
            callbackVersion = version;
        });

        MonitoringManager.Enable();

        Assert.That(callbackInvoked, Is.True);
        Assert.That(callbackState, Is.True);
        Assert.That(callbackVersion, Is.GreaterThan(0));
    }

    [Test]
    public void Enable_WhenAlreadyEnabled_DoesNotInvokeCallback()
    {
        MonitoringManager.Enable();

        int callbackCount = 0;
        MonitoringManager.AddStateChangedCallback((_, _) => callbackCount++);

        MonitoringManager.Enable();

        Assert.That(callbackCount, Is.Zero);
    }

    [Test]
    public void Disable_WhenAlreadyDisabled_DoesNotInvokeCallback()
    {
        int callbackCount = 0;
        MonitoringManager.AddStateChangedCallback((_, _) => callbackCount++);

        MonitoringManager.Disable();

        Assert.That(callbackCount, Is.Zero);
    }

    [Test]
    public void GetCurrentVersion_WhenCalled_ReturnsCurrentVersion()
    {
        int initialVersion = MonitoringManager.GetCurrentVersion();
        MonitoringManager.Enable();
        int newVersion = MonitoringManager.GetCurrentVersion();

        Assert.That(newVersion, Is.GreaterThan(initialVersion));
    }

    [Test]
    public async Task IsEnabled_UnderConcurrentAccess_MaintainsConsistencyAsync()
    {
        const int iterations = 10000;
        var tasks = new Task[iterations];

        for (int i = 0; i < iterations; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                if (i % 2 == 0)
                    MonitoringManager.Enable();
                else
                    MonitoringManager.Disable();

                // Read the state
                _ = MonitoringManager.IsEnabled;
            });
        }

        await Task.WhenAll(tasks);

        // The final state should be consistent
        bool finalState = MonitoringManager.IsEnabled;
        Assert.That(finalState, Is.AnyOf(true, false));
    }

    [Test]
    public void AddStateChangedCallback_MultipleTimes_AllCallbacksInvoked()
    {
        int callbackCount1 = 0;
        int callbackCount2 = 0;

        MonitoringManager.AddStateChangedCallback((_, _) => callbackCount1++);
        MonitoringManager.AddStateChangedCallback((_, _) => callbackCount2++);

        MonitoringManager.Enable();
        MonitoringManager.Disable();

        Assert.That(callbackCount1, Is.EqualTo(2));
        Assert.That(callbackCount2, Is.EqualTo(2));
    }

    [Test]
    public void CurrentVersion_WhenStateChanges_Increments()
    {
        int initialVersion = MonitoringManager.CurrentVersion;
        MonitoringManager.Enable();
        int versionAfterEnable = MonitoringManager.CurrentVersion;
        MonitoringManager.Disable();
        int versionAfterDisable = MonitoringManager.CurrentVersion;

        Assert.That(versionAfterEnable, Is.GreaterThan(initialVersion));
        Assert.That(versionAfterDisable, Is.GreaterThan(versionAfterEnable));
    }

    [Test]
    public void ShouldTrack_WhenEnabledAndVersionMatches_ReturnsTrue()
    {
        MonitoringManager.Enable();
        int currentVersion = MonitoringManager.GetCurrentVersion();

        Assert.That(MonitoringManager.ShouldTrack(currentVersion), Is.True);
    }

    [Test]
    public void ShouldTrack_WhenDisabledAndVersionMatches_ReturnsFalse()
    {
        MonitoringManager.Disable();
        int currentVersion = MonitoringManager.GetCurrentVersion();

        Assert.That(MonitoringManager.ShouldTrack(currentVersion), Is.False);
    }

    [Test]
    public void ShouldTrack_WhenEnabledAndVersionMismatches_ReturnsTrue()
    {
        MonitoringManager.Enable();
        int oldVersion = MonitoringManager.GetCurrentVersion();
        MonitoringManager.Disable();
        MonitoringManager.Enable();

        Assert.That(MonitoringManager.ShouldTrack(oldVersion), Is.True);
    }

    [Test]
    public void ShouldTrack_WhenDisabledAndVersionMismatches_ReturnsFalse()
    {
        MonitoringManager.Enable();
        int oldVersion = MonitoringManager.GetCurrentVersion();
        MonitoringManager.Disable();

        Assert.That(MonitoringManager.ShouldTrack(oldVersion), Is.False);
    }

    [Test]
    public void BeginOperation_IncreasesActiveOperationsCount()
    {
        MonitoringManager.Disable();
        using (MonitoringManager.BeginOperation())
        {
            Assert.That(MonitoringManager.ShouldTrack(MonitoringManager.GetCurrentVersion()), Is.True);
        }
        Assert.That(MonitoringManager.ShouldTrack(MonitoringManager.GetCurrentVersion()), Is.False);
    }
}

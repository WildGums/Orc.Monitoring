namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using System;
using Orc.Monitoring;
using Orc.Monitoring.MethodLifeCycleItems;
using System.Threading.Tasks;

[TestFixture]
public class MonitoringRobustnessTests
{
    [SetUp]
    public void Setup()
    {
        MonitoringController.ResetForTesting();
        // Ensure monitoring is not configured
        MonitoringController.Disable();
    }

    [Test]
    public void ClassMonitor_WhenMonitoringNotConfigured_ShouldNotThrowException()
    {
        Assert.DoesNotThrow(() =>
        {
            var monitor = PerformanceMonitor.ForClass<MonitoringRobustnessTests>();
            Assert.That(monitor, Is.Not.Null);
            Assert.That(monitor, Is.InstanceOf<NullClassMonitor>());
        });
    }

    [Test]
    public void StartMethod_WhenMonitoringNotConfigured_ShouldReturnDummyContext()
    {
        var monitor = PerformanceMonitor.ForClass<MonitoringRobustnessTests>();

        Assert.DoesNotThrow(() =>
        {
            using var context = monitor.Start(builder => { });
            Assert.That(context, Is.Not.Null);
            Assert.That(context, Is.EqualTo(MethodCallContext.Dummy));
        });
    }

    [Test]
    public async Task StartAsyncMethod_WhenMonitoringNotConfigured_ShouldReturnDummyContextAsync()
    {
        var monitor = PerformanceMonitor.ForClass<MonitoringRobustnessTests>();

        Assert.DoesNotThrowAsync(async () =>
        {
            await using var context = await Task.FromResult(monitor.StartAsyncMethod(new MethodConfiguration()));
            Assert.That(context, Is.Not.Null);
            Assert.That(context, Is.EqualTo(AsyncMethodCallContext.Dummy));
        });
    }

    [Test]
    public void LogStatus_WhenMonitoringNotConfigured_ShouldNotThrowException()
    {
        var monitor = PerformanceMonitor.ForClass<MonitoringRobustnessTests>();

        Assert.DoesNotThrow(() =>
        {
            monitor.LogStatus(new MockMethodLifeCycleItem());
        });
    }

    [Test]
    public void FullMethodExecution_WhenMonitoringNotConfigured_ShouldNotThrowException()
    {
        Assert.DoesNotThrow(() =>
        {
            var monitor = PerformanceMonitor.ForClass<MonitoringRobustnessTests>();
            using (var context = monitor.Start(builder => { }))
            {
                // Simulate some work
                System.Threading.Thread.Sleep(10);

                // Log some status
                context.Log("TestCategory", "Test data");

                // Simulate an exception
                context.LogException(new Exception("Test exception"));
            }
        });
    }

    private class MockMethodLifeCycleItem : IMethodLifeCycleItem
    {
        public DateTime TimeStamp => DateTime.Now;
        public MethodCallInfo MethodCallInfo => MethodCallInfo.CreateNull();
        public int ThreadId => 0;
    }
}

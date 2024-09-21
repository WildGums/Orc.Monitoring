namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using System;
using Monitoring;
using System.Threading.Tasks;
using Core.Abstractions;
using Core.CallStacks;
using Core.Configuration;
using Core.Controllers;
using Core.MethodCallContexts;
using Core.MethodLifecycle;
using Core.Models;
using Core.Monitors;
using Core.PerformanceMonitoring;
using Core.Pooling;
using TestUtilities.Logging;

[TestFixture]
public class MonitoringRobustnessTests
{
    private TestLogger<MonitoringRobustnessTests> _logger;
    private TestLoggerFactory<MonitoringRobustnessTests> _loggerFactory;
    private IMonitoringController _monitoringController;
    private IPerformanceMonitor _performanceMonitor;
    private MethodCallInfoPool _methodCallInfoPool;
    private MethodCallContextFactory _methodCallContextFactory;
    private IClassMonitorFactory _classMonitorFactory;
    private ICallStackFactory _callStackFactory;

    [SetUp]
    public void Setup()
    {
        _logger = new TestLogger<MonitoringRobustnessTests>();
        _loggerFactory = new TestLoggerFactory<MonitoringRobustnessTests>(_logger);
        _monitoringController = new MonitoringController(_loggerFactory);
        _methodCallInfoPool = new MethodCallInfoPool(_monitoringController, _loggerFactory);
        _methodCallContextFactory = new MethodCallContextFactory(_monitoringController, _loggerFactory, _methodCallInfoPool);
        _classMonitorFactory = new ClassMonitorFactory(_monitoringController, _loggerFactory, _methodCallContextFactory, _methodCallInfoPool);
        _callStackFactory = new CallStackFactory(_monitoringController, _loggerFactory, _methodCallInfoPool);

        _performanceMonitor = new PerformanceMonitor(_monitoringController, _loggerFactory,
            _callStackFactory,
            _classMonitorFactory,
            () => new ConfigurationBuilder(_monitoringController));

        // Ensure monitoring is not configured
        _monitoringController.Disable();
    }

    [Test]
    public void ClassMonitor_WhenMonitoringNotConfigured_ShouldNotThrowException()
    {
        Assert.DoesNotThrow(() =>
        {
            var monitor = _performanceMonitor.ForClass<MonitoringRobustnessTests>();
            Assert.That(monitor, Is.Not.Null);
            Assert.That(monitor, Is.InstanceOf<NullClassMonitor>());
        });
    }

    [Test]
    public void StartMethod_WhenMonitoringNotConfigured_ShouldReturnDummyContext()
    {
        var monitor = _performanceMonitor.ForClass<MonitoringRobustnessTests>();

        Assert.DoesNotThrow(() =>
        {
            using var context = monitor.Start(_ => { });
            Assert.That(context, Is.Not.Null);
            using var dummyMethodCallContext = _methodCallContextFactory.GetDummyMethodCallContext();
            Assert.That(context, Is.EqualTo(dummyMethodCallContext));
        });
    }

    [Test]
    public async Task StartAsyncMethod_WhenMonitoringNotConfigured_ShouldReturnDummyContextAsync()
    {
        var monitor = _performanceMonitor.ForClass<MonitoringRobustnessTests>();

        Assert.DoesNotThrowAsync(async () =>
        {
            await using var context = await Task.FromResult(monitor.StartAsyncMethod(new MethodConfiguration()));
            Assert.That(context, Is.Not.Null);

            await using var dummyAsyncMethodCallContext = _methodCallContextFactory.GetDummyAsyncMethodCallContext();

            Assert.That(context.GetType(), Is.EqualTo(dummyAsyncMethodCallContext.GetType()), "Context types should match");
            Assert.That(context.MethodCallInfo, Is.EqualTo(dummyAsyncMethodCallContext.MethodCallInfo), "MethodCallInfo should match");
            Assert.That(context.ReporterIds, Is.EquivalentTo(dummyAsyncMethodCallContext.ReporterIds), "ReporterIds should match");
        });
    }

    [Test]
    public void LogStatus_WhenMonitoringNotConfigured_ShouldNotThrowException()
    {
        var monitor = _performanceMonitor.ForClass<MonitoringRobustnessTests>();

        Assert.DoesNotThrow(() =>
        {
            monitor.LogStatus(new MockMethodLifeCycleItem(_methodCallInfoPool));
        });
    }

    [Test]
    public void FullMethodExecution_WhenMonitoringNotConfigured_ShouldNotThrowException()
    {
        Assert.DoesNotThrow(() =>
        {
            var monitor = _performanceMonitor.ForClass<MonitoringRobustnessTests>();
            using var context = monitor.Start(_ => { });
            // Simulate some work
            System.Threading.Thread.Sleep(10);

            // Log some status
            context.Log("TestCategory", "Test data");

            // Simulate an exception
            context.LogException(new Exception("Test exception"));
        });
    }

    [Test]
    public async Task FullMethodExecution_WhenMonitoringNotConfigured_ShouldNotThrowExceptionAsync()
    {
        Assert.DoesNotThrowAsync(async () =>
        {
            var monitor = _performanceMonitor.ForClass<MonitoringRobustnessTests>();
            await using var context = monitor.AsyncStart(_ => { });
            // Simulate some work
            System.Threading.Thread.Sleep(10);

            // Log some status
            context.Log("TestCategory", "Test data");

            // Simulate an exception
            context.LogException(new Exception("Test exception"));
        });
    }

    private class MockMethodLifeCycleItem(MethodCallInfoPool methodCallInfoPool) : IMethodLifeCycleItem
    {
        public DateTime TimeStamp => DateTime.Now;
        public MethodCallInfo MethodCallInfo => methodCallInfoPool.GetNull();
        public int ThreadId => 0;
    }
}

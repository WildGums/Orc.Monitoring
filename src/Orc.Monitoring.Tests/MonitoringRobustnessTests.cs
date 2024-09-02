namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using System;
using Orc.Monitoring;
using Orc.Monitoring.MethodLifeCycleItems;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Reporters;

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
        _monitoringController = new MonitoringController(_loggerFactory, () => new EnhancedDataPostProcessor(_loggerFactory));
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
            using var context = monitor.Start(builder => { });
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
            Assert.That(context, Is.EqualTo(dummyAsyncMethodCallContext));
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
        private readonly MethodCallInfoPool _methodCallInfoPool;

        public MockMethodLifeCycleItem(MethodCallInfoPool methodCallInfoPool)
        {
            _methodCallInfoPool = methodCallInfoPool;
        }

        public DateTime TimeStamp => DateTime.Now;
        public MethodCallInfo MethodCallInfo => _methodCallInfoPool.GetNull();
        public int ThreadId => 0;
    }
}

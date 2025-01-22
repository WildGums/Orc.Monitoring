// ReSharper disable NotNullOrRequiredMemberIsNotInitialized
// ReSharper disable InconsistentNaming
#pragma warning disable IDE1006
#pragma warning disable CA1822
#pragma warning disable CL0002
namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using System;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Extensions.Logging;
using Reporters;
using System.Linq;
using TestUtilities.Filters;
using TestUtilities.Logging;
using TestUtilities.Mocks;

[TestFixture]
public class PerformanceMonitorIntegrationTests
{
    private MockReporter _mockReporter;
    private TestLogger<PerformanceMonitorIntegrationTests> _logger;
    private TestLoggerFactory<PerformanceMonitorIntegrationTests> _loggerFactory;
    private IMonitoringController _monitoringController;
    private IPerformanceMonitor _performanceMonitor;
    private MethodCallContextFactory _methodCallContextFactory;
    private MethodCallInfoPool _methodCallInfoPool;
    private IClassMonitorFactory _classMonitorFactory;
    private ICallStackFactory _callStackFactory;

    [SetUp]
    public void Setup()
    {
        _logger = new TestLogger<PerformanceMonitorIntegrationTests>();
        _loggerFactory = new TestLoggerFactory<PerformanceMonitorIntegrationTests>(_logger);
        _monitoringController = new MonitoringController(_loggerFactory);

        _methodCallInfoPool = new MethodCallInfoPool(_monitoringController, _loggerFactory);
        _methodCallContextFactory = new MethodCallContextFactory(_monitoringController, _loggerFactory, _methodCallInfoPool);

        _classMonitorFactory = new ClassMonitorFactory(_monitoringController, _loggerFactory, _methodCallContextFactory, _methodCallInfoPool);
        _callStackFactory = new CallStackFactory(_monitoringController, _loggerFactory, _methodCallInfoPool);

        _performanceMonitor = new PerformanceMonitor(_monitoringController, _loggerFactory,
            _callStackFactory,
            _classMonitorFactory,
            () => new ConfigurationBuilder(_monitoringController));

        _mockReporter = new MockReporter(_loggerFactory) { Id = "TestReporter" };

        _performanceMonitor.Configure(config =>
        {
            config.TrackAssembly(typeof(PerformanceMonitorIntegrationTests).Assembly);
            config.AddFilter(new AlwaysIncludeFilter(_loggerFactory));
            config.AddReporterType(typeof(MockReporter));
        });

        _monitoringController.Enable();
        _monitoringController.EnableReporter(typeof(MockReporter));
    }

    [TearDown]
    public void TearDown()
    {
        _monitoringController.Disable();
    }

    [Test]
    public async Task ComplexAsyncWorkflow_IsTrackedCorrectly()
    {
        using var cts = new CancellationTokenSource();
        var testClass = new TestClassWithAsyncMethods(_performanceMonitor, _loggerFactory, _mockReporter);

        try
        {
            var task = testClass.ComplexAsyncWorkflowAsync(cts.Token);
            await Task.Delay(2000, cts.Token); // Give some time for the workflow to progress
            await cts.CancelAsync();
            await task;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        Assert.That(_mockReporter.CallCount, Is.GreaterThan(0), "Expected multiple method calls to be tracked");
        Assert.That(_mockReporter.OperationSequence, Does.Contain("StartReporting"), "StartReporting should have been called");
        Assert.That(_mockReporter.OperationSequence, Does.Contain("SetRootMethod"), "SetRootMethod should have been called");
    }

    [Test]
    public async Task ParallelAsyncOperations_AreTrackedIndependently()
    {
        var testClass = new TestClassWithAsyncMethods(_performanceMonitor, _loggerFactory, _mockReporter);

        var task1 = testClass.AsyncOperation1Async();
        var task2 = testClass.AsyncOperation2Async();

        await Task.WhenAll(task1, task2);

        Assert.That(_mockReporter.CallCount, Is.EqualTo(2), "Expected two method calls to be tracked");
        Assert.That(_mockReporter.OperationSequence.Count(s => s == "StartReporting"), Is.EqualTo(2), "StartReporting should have been called twice");
    }

    [Test]
    public void NestedSyncMethods_AreTrackedCorrectly()
    {
        var testClass = new TestClassWithNestedMethods(_performanceMonitor, _loggerFactory, _mockReporter);

        testClass.OuterMethod();

        Assert.That(_mockReporter.CallCount, Is.EqualTo(3), "Expected three method calls to be tracked");
        Assert.That(_mockReporter.OperationSequence, Does.Contain("SetRootMethod"), "SetRootMethod should have been called");
        Assert.That(_mockReporter.OperationSequence.Count(s => s == "StartReporting"), Is.EqualTo(3), "StartReporting should have been called three times");
    }

    [Test]
    public async Task ExceptionHandling_InAsyncMethod_IsTrackedCorrectly()
    {
        var testClass = new TestClassWithAsyncMethods(_performanceMonitor, _loggerFactory, _mockReporter);

        Assert.ThrowsAsync<InvalidOperationException>(async () => await testClass.AsyncMethodWithExceptionAsync());

        Assert.That(_mockReporter.CallCount, Is.EqualTo(1), "Expected one method call to be tracked");
        Assert.That(_mockReporter.OperationSequence, Does.Contain("StartReporting"), "StartReporting should have been called");
        // Add more specific assertions about exception tracking if your implementation supports it
    }

    [Test]
    public void MonitoringToggle_AffectsTracking()
    {
        var testClass = new TestClassWithSyncMethods(_performanceMonitor, _loggerFactory, _mockReporter);

        testClass.SyncMethod();
        Assert.That(_mockReporter.CallCount, Is.EqualTo(1), "First call should be tracked");

        _monitoringController.Disable();
        testClass.SyncMethod();
        Assert.That(_mockReporter.CallCount, Is.EqualTo(1), "Call while disabled should not be tracked");

        _monitoringController.Enable();
        testClass.SyncMethod();
        Assert.That(_mockReporter.CallCount, Is.EqualTo(2), "Call after re-enabling should be tracked");
    }

    private class TestClassWithAsyncMethods(IPerformanceMonitor performanceMonitor, IMonitoringLoggerFactory loggerFactory, IMethodCallReporter reporter)
    {
        private readonly IClassMonitor _monitor = performanceMonitor.ForClass<TestClassWithAsyncMethods>();
        private readonly ILogger<TestClassWithAsyncMethods> _logger = loggerFactory.CreateLogger<TestClassWithAsyncMethods>();

        public async Task ComplexAsyncWorkflowAsync(CancellationToken cancellationToken)
        {
            await using var context = _monitor.AsyncStart(builder => builder.AddReporter(reporter));
            try
            {
                _logger.LogInformation("Starting complex async workflow");
                await Task.Delay(500, cancellationToken);
                await AsyncOperation1Async();
                await Task.Delay(500, cancellationToken);
                await AsyncOperation2Async();
                _logger.LogInformation("Complex async workflow completed");
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Complex async workflow was canceled");
                throw;
            }
        }

        public async Task AsyncOperation1Async()
        {
            await using var context = _monitor.AsyncStart(builder => builder.AddReporter(reporter));
            _logger.LogInformation("Async Operation 1 started");
            await Task.Delay(1000);
            _logger.LogInformation("Async Operation 1 completed");
        }

        public async Task AsyncOperation2Async()
        {
            await using var context = _monitor.AsyncStart(builder => builder.AddReporter(reporter));
            _logger.LogInformation("Async Operation 2 started");
            await Task.Delay(1500);
            _logger.LogInformation("Async Operation 2 completed");
        }

        public async Task AsyncMethodWithExceptionAsync()
        {
            await using var context = _monitor.AsyncStart(builder => builder.AddReporter(reporter));
            _logger.LogInformation("Async method with exception started");
            await Task.Delay(500);
            throw new InvalidOperationException("Test exception");
        }
    }

    private class TestClassWithNestedMethods(IPerformanceMonitor performanceMonitor, IMonitoringLoggerFactory loggerFactory, IMethodCallReporter reporter)
    {
        private readonly IClassMonitor _monitor = performanceMonitor.ForClass<TestClassWithNestedMethods>();
        private readonly ILogger<TestClassWithNestedMethods> _logger = loggerFactory.CreateLogger<TestClassWithNestedMethods>();

        public void OuterMethod()
        {
            using var context = _monitor.Start(builder => builder.AddReporter(reporter));
            _logger.LogInformation("Outer method started");
            MiddleMethod();
            _logger.LogInformation("Outer method completed");
        }

        private void MiddleMethod()
        {
            using var context = _monitor.Start(builder => builder.AddReporter(reporter));
            _logger.LogInformation("Middle method started");
            InnerMethod();
            _logger.LogInformation("Middle method completed");
        }

        private void InnerMethod()
        {
            using var context = _monitor.Start(builder => builder.AddReporter(reporter));
            _logger.LogInformation("Inner method started");
            Thread.Sleep(100);
            _logger.LogInformation("Inner method completed");
        }
    }

    private class TestClassWithSyncMethods(IPerformanceMonitor performanceMonitor, IMonitoringLoggerFactory loggerFactory, IMethodCallReporter reporter)
    {
        private readonly IClassMonitor _monitor = performanceMonitor.ForClass<TestClassWithSyncMethods>();
        private readonly ILogger<TestClassWithSyncMethods> _logger = loggerFactory.CreateLogger<TestClassWithSyncMethods>();

        public void SyncMethod()
        {
            using var context = _monitor.Start(builder => builder.AddReporter(reporter));
            _logger.LogInformation("Sync method started");
            Thread.Sleep(100);
            _logger.LogInformation("Sync method completed");
        }
    }
}

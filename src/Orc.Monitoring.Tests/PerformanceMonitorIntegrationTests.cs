// ReSharper disable NotNullOrRequiredMemberIsNotInitialized
// ReSharper disable InconsistentNaming
#pragma warning disable IDE1006
#pragma warning disable CA1822
#pragma warning disable CL0002
namespace Orc.Monitoring.Tests;

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Reporters;
using Reporters.ReportOutputs;

[TestFixture]
public class PerformanceMonitorIntegrationTests
{
    private MockReporter _mockReporter;
    private readonly string reporterId = "TestReporter";
    private TestLogger<PerformanceMonitorIntegrationTests> _logger;
    private TestLoggerFactory<PerformanceMonitorIntegrationTests> _loggerFactory;
    private IMonitoringController _monitoringController;
    private IPerformanceMonitor _performanceMonitor;
    private MethodCallContextFactory _methodCallContextFactory;
    private MethodCallInfoPool _methodCallInfoPool;

    private class TestClass
    {
        private readonly IClassMonitor _monitor;
        private readonly ILogger<TestClass> _logger;
        private readonly IPerformanceMonitor _performanceMonitor;
        private readonly IMethodCallReporter _reporter;

        public TestClass(IPerformanceMonitor performanceMonitor, IMonitoringLoggerFactory loggerFactory, IMethodCallReporter reporter)
        {
            ArgumentNullException.ThrowIfNull(loggerFactory);
            ArgumentNullException.ThrowIfNull(reporter);

            _logger = loggerFactory.CreateLogger<TestClass>();
            _performanceMonitor = performanceMonitor;
            _reporter = reporter;
            _logger.LogInformation("TestClass static constructor called");
            _monitor = _performanceMonitor.ForClass<TestClass>();
            _logger.LogInformation($"Monitor created for TestClass: {_monitor.GetType().Name}");
        }

        public void TestMethod()
        {
            _logger.LogInformation("TestClass.TestMethod entered");
            _logger.LogInformation($"Using monitor of type: {_monitor.GetType().Name}");
            using var context = _monitor.StartMethod(new MethodConfiguration
            {
                Reporters = [_reporter]
            });
            _logger.LogInformation("TestClass.TestMethod executing");
            _logger.LogInformation("TestClass.TestMethod exited");
        }

        public async Task TestAsyncMethod()
        {
            _logger.LogInformation("TestClass.TestAsyncMethod entered");
            _logger.LogInformation($"Using monitor of type: {_monitor.GetType().Name}");
            await using var context = _monitor.StartAsyncMethod(new MethodConfiguration
            {
                Reporters = [_reporter]
            });
            _logger.LogInformation("TestClass.TestAsyncMethod executing");
            await Task.Delay(10);
            _logger.LogInformation("TestClass.TestAsyncMethod exited");
        }
    }

    private class TestClass2
    {
        public  void TestMethod()
        {
        }
    }

    [SetUp]
    public void Setup()
    {
        _logger = new TestLogger<PerformanceMonitorIntegrationTests>();
        _loggerFactory = new TestLoggerFactory<PerformanceMonitorIntegrationTests>(_logger);
        _monitoringController = new MonitoringController(_loggerFactory, () => new EnhancedDataPostProcessor(_loggerFactory));

        _methodCallInfoPool = new MethodCallInfoPool(_monitoringController, _loggerFactory);
        _methodCallContextFactory = new MethodCallContextFactory(_monitoringController, _loggerFactory, _methodCallInfoPool);
        _performanceMonitor = new PerformanceMonitor(_monitoringController, _loggerFactory,
            (config) => new CallStack(_monitoringController, config, _methodCallInfoPool, _loggerFactory),
            (type, callStack, config) => new ClassMonitor(_monitoringController, type, callStack, config, _loggerFactory, _methodCallContextFactory, _methodCallInfoPool),
            () => new ConfigurationBuilder(_monitoringController));

        _logger.LogInformation("Setup started");

        _mockReporter = new MockReporter(_loggerFactory) { Id = reporterId };
        _logger.LogInformation($"Setup completed at {DateTime.Now:HH:mm:ss.fff}");
        _mockReporter.Reset();

        _performanceMonitor.Configure(config =>
        {
            _logger.LogInformation($"Configuring PerformanceMonitor. Tracking assembly: {typeof(TestClass).Assembly.FullName}");
            config.TrackAssembly(typeof(TestClass).Assembly);
            var filter = new AlwaysIncludeFilter(_loggerFactory);
            config.AddFilter(filter);
            config.AddReporterType(typeof(MockReporter));
            _logger.LogInformation($"Added AlwaysIncludeFilter: {filter.GetType().FullName}");
            _logger.LogInformation($"Added MockReporter with ID: {reporterId}");
        });

        _monitoringController.Enable();
        _logger.LogInformation($"Monitoring enabled: {_monitoringController.IsEnabled}");
        _monitoringController.EnableReporter(typeof(MockReporter));
        _logger.LogInformation("Setup completed");

        // Force re-creation of TestClass monitor
        typeof(TestClass).TypeInitializer?.Invoke(null, null);
    }

    [TearDown]
    public void Teardown()
    {
        _logger.LogInformation($"TearDown started at {DateTime.Now:HH:mm:ss.fff}");
        _logger.LogInformation($"Final CallCount: {_mockReporter.CallCount}");
    }


    [Test]
    public void ForClass_WithFilterButNoExplicitMethods_ShouldTrackAllMethods()
    {
        // Arrange
        _logger.LogInformation("Starting test setup");

        _monitoringController.Enable();
        _logger.LogInformation($"Monitoring enabled: {_monitoringController.IsEnabled}");

        var reporterId = "TestReporter";
        _performanceMonitor.Configure(config =>
        {
            _logger.LogInformation($"Configuring PerformanceMonitor. Tracking assembly: {typeof(TestClass2).Assembly.FullName}");
            config.TrackAssembly(typeof(TestClass2).Assembly);
            var filter = new AlwaysIncludeFilter(_loggerFactory);
            config.AddFilter(filter);
            config.AddReporterType(typeof(MockReporter));
            _logger.LogInformation($"Added AlwaysIncludeFilter: {filter.GetType().FullName}");
            _logger.LogInformation($"Added MockReporter with ID: {reporterId}");
        });

        _performanceMonitor.LogCurrentConfiguration();

        // Explicitly enable the reporter and filter
        _monitoringController.EnableReporter(typeof(MockReporter));
        _monitoringController.EnableFilter(typeof(AlwaysIncludeFilter));
        _monitoringController.EnableFilterForReporterType(typeof(MockReporter), typeof(AlwaysIncludeFilter));
        _logger.LogInformation($"MockReporter enabled: {_monitoringController.IsReporterEnabled(typeof(MockReporter))}");
        _logger.LogInformation($"AlwaysIncludeFilter enabled: {_monitoringController.IsFilterEnabled(typeof(AlwaysIncludeFilter))}");
        _logger.LogInformation($"AlwaysIncludeFilter enabled for MockReporter: {_monitoringController.IsFilterEnabledForReporterType(typeof(MockReporter), typeof(AlwaysIncludeFilter))}");

        // Act
        _logger.LogInformation("Creating monitor for TestClass2");
        var monitor = _performanceMonitor.ForClass<TestClass2>();
        _logger.LogInformation($"Monitor type: {monitor.GetType().Name}");

        // Assert
        _logger.LogInformation("Starting method and creating context");
        using (var context = monitor.Start(builder =>
               {
                   builder.AddReporter(new MockReporter(_loggerFactory) { Id = reporterId });
                   _logger.LogInformation($"Added MockReporter with ID: {reporterId} to method configuration");
               }, nameof(TestClass2.TestMethod)))
        {
            _logger.LogInformation($"Context type: {context.GetType().Name}");
            using var dummyMethodCallContext = _methodCallContextFactory.GetDummyMethodCallContext();
            Assert.That(context, Is.Not.EqualTo(dummyMethodCallContext),
                "The context should not be a Dummy context when the filter includes all methods");
        }
    }

    [Test]
    public void WhenMonitoringIsEnabled_MethodsAreTracked()
    {
        var testClass = new TestClass(_performanceMonitor, _loggerFactory, _mockReporter);
        testClass.TestMethod();
        _logger.LogInformation($"CallCount after method execution: {_mockReporter.CallCount}");
        Assert.That(_mockReporter.CallCount, Is.EqualTo(1), "Expected 1 call for the sync method");
    }

    [Test]
    public async Task WhenMonitoringIsEnabled_AsyncMethodsAreTracked()
    {
        _logger.LogInformation("Async test started");
        var testClass = new TestClass(_performanceMonitor, _loggerFactory, _mockReporter);
        await testClass.TestAsyncMethod();
        _logger.LogInformation($"Final CallCount: {_mockReporter.CallCount}");
        Assert.That(_mockReporter.CallCount, Is.EqualTo(1), "Expected 1 call for the async method");
    }

    [Test]
    public void WhenMonitoringIsDisabled_MethodsAreNotTracked()
    {
        _monitoringController.Disable();
        var testClass = new TestClass(_performanceMonitor, _loggerFactory, _mockReporter);
        testClass.TestMethod();
        Assert.That(_mockReporter.CallCount, Is.EqualTo(0), "Expected no calls when monitoring is disabled");
    }

    [Test]
    public async Task WhenMonitoringIsDisabled_AsyncMethodsAreNotTracked()
    {
        _monitoringController.Disable();
        var testClass = new TestClass(_performanceMonitor, _loggerFactory, _mockReporter);
        await testClass.TestAsyncMethod();
        Assert.That(_mockReporter.CallCount, Is.EqualTo(0), "Expected no calls when monitoring is disabled");
    }

    [Test]
    public void WhenMonitoringIsToggled_TrackingRespondsAccordingly()
    {
        var testClass = new TestClass(_performanceMonitor, _loggerFactory, _mockReporter);

        _logger.LogInformation("Running first method call with monitoring enabled");
        testClass.TestMethod();
        _logger.LogInformation($"CallCount after first call: {_mockReporter.CallCount}");
        Assert.That(_mockReporter.CallCount, Is.EqualTo(1), "Expected 1 call when monitoring is enabled");

        _logger.LogInformation("Disabling monitoring");
        _monitoringController.Disable();
        testClass.TestMethod();
        _logger.LogInformation($"CallCount after second call (monitoring disabled): {_mockReporter.CallCount}");
        Assert.That(_mockReporter.CallCount, Is.EqualTo(1), "Expected no additional calls when monitoring is disabled");

        _logger.LogInformation("Re-enabling monitoring");
        _monitoringController.Enable();
        testClass.TestMethod();
        _logger.LogInformation($"CallCount after third call (monitoring re-enabled): {_mockReporter.CallCount}");
        Assert.That(_mockReporter.CallCount, Is.EqualTo(2), "Expected 1 additional call when monitoring is re-enabled");
    }

    [Test]
    public void WhenMonitoringIsDisabledMidMethod_MethodCompletesTracking()
    {
        _logger.LogInformation("Test started");
        var testClass = new TestClass(_performanceMonitor, _loggerFactory, _mockReporter);

        _logger.LogInformation("Calling TestMethod first time");
        testClass.TestMethod();

        _logger.LogInformation("Setting up callback");
        _monitoringController.AddStateChangedCallback((componentType, componentName, isEnabled, version) =>
        {
            _logger.LogInformation($"State changed callback. Component: {componentType}, Name: {componentName}, Enabled: {isEnabled}, Version: {version}");
            if (!isEnabled)
            {
                _logger.LogInformation("Calling TestMethod from callback");
                testClass.TestMethod();
            }
        });

        _logger.LogInformation("Calling TestMethod second time");
        testClass.TestMethod();

        _logger.LogInformation("Disabling monitoring");
        _monitoringController.Disable();

        _logger.LogInformation($"Final CallCount: {_mockReporter.CallCount}");
        Assert.That(_mockReporter.CallCount, Is.EqualTo(2), "Expected 2 calls: 1 before disabling, 1 after disabling");
    }

    [Test, Retry(3)] // Retry up to 3 times
    public async Task WhenMonitoringIsDisabledMidAsyncMethod_MethodCompletesTracking()
    {
        _logger.LogInformation("Test started");
        var testClass = new TestClass(_performanceMonitor, _loggerFactory, _mockReporter);

        var task = testClass.TestAsyncMethod();

        // Give some time for the method to start
        await Task.Delay(5);

        _logger.LogInformation("Disabling monitoring");
        _monitoringController.Disable();

        await task;

        _logger.LogInformation($"Final CallCount: {_mockReporter.CallCount}");
        Assert.That(_mockReporter.CallCount, Is.EqualTo(1), "Expected 1 call: method started before disabling should complete");
    }

    [Test]
    public void Configure_EnablesDefaultOutputTypes()
    {
        _performanceMonitor.Configure(_ => { });

        Assert.Multiple(() =>
        {
            Assert.That(_monitoringController.IsOutputTypeEnabled<CsvReportOutput>(), Is.False);
            Assert.That(_monitoringController.IsOutputTypeEnabled<RanttOutput>(), Is.True);
            Assert.That(_monitoringController.IsOutputTypeEnabled<TxtReportOutput>(), Is.True);
        });
    }

    [Test]
    public void Configure_AllowsCustomOutputTypeConfiguration()
    {
        _performanceMonitor.Configure(config =>
        {
            config.SetOutputTypeState<CsvReportOutput>(false);
            config.SetOutputTypeState<RanttOutput>(true);
        });

        Assert.Multiple(() =>
        {
            Assert.That(_monitoringController.IsOutputTypeEnabled<CsvReportOutput>(), Is.False);
            Assert.That(_monitoringController.IsOutputTypeEnabled<RanttOutput>(), Is.True);
            // Check other output types are still enabled by default
            Assert.That(_monitoringController.IsOutputTypeEnabled<TxtReportOutput>(), Is.True);
        });
    }
}

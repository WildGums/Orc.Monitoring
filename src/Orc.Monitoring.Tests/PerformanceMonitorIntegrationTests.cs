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
using NUnit.Framework.Internal;
using Reporters.ReportOutputs;

[TestFixture]
public class PerformanceMonitorIntegrationTests
{
    private static MockReporter _mockReporter;
    private static readonly string reporterId = "TestReporter";

    private class TestClass
    {
        private static readonly IClassMonitor _monitor;

        static TestClass()
        {
            Console.WriteLine("TestClass static constructor called");
            _monitor = PerformanceMonitor.ForClass<TestClass>();
            Console.WriteLine($"Monitor created for TestClass: {_monitor.GetType().Name}");
        }

        public void TestMethod()
        {
            Console.WriteLine("TestClass.TestMethod entered");
            Console.WriteLine($"Using monitor of type: {_monitor.GetType().Name}");
            using var context = _monitor.StartMethod(new MethodConfiguration
            {
                Reporters = [_mockReporter]
            });
            Console.WriteLine("TestClass.TestMethod executing");
            Console.WriteLine("TestClass.TestMethod exited");
        }

        public async Task TestAsyncMethod()
        {
            Console.WriteLine("TestClass.TestAsyncMethod entered");
            Console.WriteLine($"Using monitor of type: {_monitor.GetType().Name}");
            await using var context = _monitor.StartAsyncMethod(new MethodConfiguration
            {
                Reporters = [_mockReporter]
            });
            Console.WriteLine("TestClass.TestAsyncMethod executing");
            await Task.Delay(10);
            Console.WriteLine("TestClass.TestAsyncMethod exited");
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
        Console.WriteLine("Setup started");

        MonitoringController.ResetForTesting();

        _mockReporter = new MockReporter { Id = reporterId };
        Console.WriteLine($"Setup completed at {DateTime.Now:HH:mm:ss.fff}"); _mockReporter.Reset(); 

        PerformanceMonitor.Configure(config =>
        {
            Console.WriteLine($"Configuring PerformanceMonitor. Tracking assembly: {typeof(TestClass).Assembly.FullName}");
            config.TrackAssembly(typeof(TestClass).Assembly);
            var filter = new AlwaysIncludeFilter();
            config.AddFilter(filter);
            config.AddReporter(typeof(MockReporter));
            Console.WriteLine($"Added AlwaysIncludeFilter: {filter.GetType().FullName}");
            Console.WriteLine($"Added MockReporter with ID: {reporterId}");
        });

        MonitoringController.Enable();
        Console.WriteLine($"Monitoring enabled: {MonitoringController.IsEnabled}");
        MonitoringController.EnableReporter(typeof(MockReporter));
        Console.WriteLine("Setup completed");

        // Force re-creation of TestClass monitor
        typeof(TestClass).TypeInitializer?.Invoke(null, null);
    }

    [TearDown]
    public void Teardown()
    {
        Console.WriteLine($"TearDown started at {DateTime.Now:HH:mm:ss.fff}");
        Console.WriteLine($"Final CallCount: {_mockReporter.CallCount}");
    }


    [Test]
    public void ForClass_WithFilterButNoExplicitMethods_ShouldTrackAllMethods()
    {
        var logger = MonitoringController.CreateLogger<PerformanceMonitorIntegrationTests>();

        // Arrange
        logger.LogInformation("Starting test setup");
        MonitoringController.ResetForTesting();
        MonitoringController.Enable();
        logger.LogInformation($"Monitoring enabled: {MonitoringController.IsEnabled}");

        var reporterId = "TestReporter";
        PerformanceMonitor.Configure(config =>
        {
            logger.LogInformation($"Configuring PerformanceMonitor. Tracking assembly: {typeof(TestClass2).Assembly.FullName}");
            config.TrackAssembly(typeof(TestClass2).Assembly);
            var filter = new AlwaysIncludeFilter();
            config.AddFilter(filter);
            config.AddReporter(typeof(MockReporter));
            logger.LogInformation($"Added AlwaysIncludeFilter: {filter.GetType().FullName}");
            logger.LogInformation($"Added MockReporter with ID: {reporterId}");
        });

        PerformanceMonitor.LogCurrentConfiguration();

        // Explicitly enable the reporter and filter
        MonitoringController.EnableReporter(typeof(MockReporter));
        MonitoringController.EnableFilter(typeof(AlwaysIncludeFilter));
        MonitoringController.EnableFilterForReporterType(typeof(MockReporter), typeof(AlwaysIncludeFilter));
        logger.LogInformation($"MockReporter enabled: {MonitoringController.IsReporterEnabled(typeof(MockReporter))}");
        logger.LogInformation($"AlwaysIncludeFilter enabled: {MonitoringController.IsFilterEnabled(typeof(AlwaysIncludeFilter))}");
        logger.LogInformation($"AlwaysIncludeFilter enabled for MockReporter: {MonitoringController.IsFilterEnabledForReporterType(typeof(MockReporter), typeof(AlwaysIncludeFilter))}");

        // Act
        logger.LogInformation("Creating monitor for TestClass2");
        var monitor = PerformanceMonitor.ForClass<TestClass2>();
        logger.LogInformation($"Monitor type: {monitor.GetType().Name}");

        // Assert
        logger.LogInformation("Starting method and creating context");
        using (var context = monitor.Start(builder =>
        {
            builder.AddReporter(new MockReporter { Id = reporterId });
            logger.LogInformation($"Added MockReporter with ID: {reporterId} to method configuration");
        }, nameof(TestClass2.TestMethod)))
        {
            logger.LogInformation($"Context type: {context.GetType().Name}");
            Assert.That(context, Is.Not.EqualTo(MethodCallContext.Dummy),
                "The context should not be a Dummy context when the filter includes all methods");
        }
    }

    [Test]
    public void WhenMonitoringIsEnabled_MethodsAreTracked()
    {
        var testClass = new TestClass();
        testClass.TestMethod();
        Console.WriteLine($"CallCount after method execution: {_mockReporter.CallCount}");
        Assert.That(_mockReporter.CallCount, Is.EqualTo(1), "Expected 1 call for the sync method");
    }

    [Test]
    public async Task WhenMonitoringIsEnabled_AsyncMethodsAreTracked()
    {
        Console.WriteLine("Async test started");
        var testClass = new TestClass();
        await testClass.TestAsyncMethod();
        Console.WriteLine($"Final CallCount: {_mockReporter.CallCount}");
        Assert.That(_mockReporter.CallCount, Is.EqualTo(1), "Expected 1 call for the async method");
    }

    [Test]
    public void WhenMonitoringIsDisabled_MethodsAreNotTracked()
    {
        MonitoringController.Disable();
        var testClass = new TestClass();
        testClass.TestMethod();
        Assert.That(_mockReporter.CallCount, Is.EqualTo(0), "Expected no calls when monitoring is disabled");
    }

    [Test]
    public async Task WhenMonitoringIsDisabled_AsyncMethodsAreNotTracked()
    {
        MonitoringController.Disable();
        var testClass = new TestClass();
        await testClass.TestAsyncMethod();
        Assert.That(_mockReporter.CallCount, Is.EqualTo(0), "Expected no calls when monitoring is disabled");
    }

    [Test]
    public void WhenMonitoringIsToggled_TrackingRespondsAccordingly()
    {
        var testClass = new TestClass();

        Console.WriteLine("Running first method call with monitoring enabled");
        testClass.TestMethod();
        Console.WriteLine($"CallCount after first call: {_mockReporter.CallCount}");
        Assert.That(_mockReporter.CallCount, Is.EqualTo(1), "Expected 1 call when monitoring is enabled");

        Console.WriteLine("Disabling monitoring");
        MonitoringController.Disable();
        testClass.TestMethod();
        Console.WriteLine($"CallCount after second call (monitoring disabled): {_mockReporter.CallCount}");
        Assert.That(_mockReporter.CallCount, Is.EqualTo(1), "Expected no additional calls when monitoring is disabled");

        Console.WriteLine("Re-enabling monitoring");
        MonitoringController.Enable();
        testClass.TestMethod();
        Console.WriteLine($"CallCount after third call (monitoring re-enabled): {_mockReporter.CallCount}");
        Assert.That(_mockReporter.CallCount, Is.EqualTo(2), "Expected 1 additional call when monitoring is re-enabled");
    }

    [Test]
    public void WhenMonitoringIsDisabledMidMethod_MethodCompletesTracking()
    {
        Console.WriteLine("Test started");
        var testClass = new TestClass();

        Console.WriteLine("Calling TestMethod first time");
        testClass.TestMethod();

        Console.WriteLine("Setting up callback");
        MonitoringController.AddStateChangedCallback((componentType, componentName, isEnabled, version) =>
        {
            Console.WriteLine($"State changed callback. Component: {componentType}, Name: {componentName}, Enabled: {isEnabled}, Version: {version}");
            if (!isEnabled)
            {
                Console.WriteLine("Calling TestMethod from callback");
                testClass.TestMethod();
            }
        });

        Console.WriteLine("Calling TestMethod second time");
        testClass.TestMethod();

        Console.WriteLine("Disabling monitoring");
        MonitoringController.Disable();

        Console.WriteLine($"Final CallCount: {_mockReporter.CallCount}");
        Assert.That(_mockReporter.CallCount, Is.EqualTo(2), "Expected 2 calls: 1 before disabling, 1 after disabling");
    }

    [Test, Retry(3)] // Retry up to 3 times
    public async Task WhenMonitoringIsDisabledMidAsyncMethod_MethodCompletesTracking()
    {
        Console.WriteLine("Test started");
        var testClass = new TestClass();

        var task = testClass.TestAsyncMethod();

        // Give some time for the method to start
        await Task.Delay(5);

        Console.WriteLine("Disabling monitoring");
        MonitoringController.Disable();

        await task;

        Console.WriteLine($"Final CallCount: {_mockReporter.CallCount}");
        Assert.That(_mockReporter.CallCount, Is.EqualTo(1), "Expected 1 call: method started before disabling should complete");
    }

    [Test]
    public void Configure_EnablesDefaultOutputTypes()
    {
        PerformanceMonitor.Configure(_ => { });

        Assert.Multiple(() =>
        {
            Assert.That(MonitoringController.IsOutputTypeEnabled<CsvReportOutput>(), Is.False);
            Assert.That(MonitoringController.IsOutputTypeEnabled<RanttOutput>(), Is.True);
            Assert.That(MonitoringController.IsOutputTypeEnabled<TxtReportOutput>(), Is.True);
        });
    }

    [Test]
    public void Configure_AllowsCustomOutputTypeConfiguration()
    {
        PerformanceMonitor.Configure(config =>
        {
            config.SetOutputTypeState<CsvReportOutput>(false);
            config.SetOutputTypeState<RanttOutput>(true);
        });

        Assert.Multiple(() =>
        {
            Assert.That(MonitoringController.IsOutputTypeEnabled<CsvReportOutput>(), Is.False);
            Assert.That(MonitoringController.IsOutputTypeEnabled<RanttOutput>(), Is.True);
            // Check other output types are still enabled by default
            Assert.That(MonitoringController.IsOutputTypeEnabled<TxtReportOutput>(), Is.True);
        });
    }
}

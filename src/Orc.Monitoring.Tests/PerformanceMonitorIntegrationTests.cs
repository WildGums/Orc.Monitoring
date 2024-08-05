// ReSharper disable NotNullOrRequiredMemberIsNotInitialized
// ReSharper disable InconsistentNaming
#pragma warning disable IDE1006
#pragma warning disable CA1822
#pragma warning disable CL0002
namespace Orc.Monitoring.Tests;

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using Orc.Monitoring.MethodLifeCycleItems;
using Orc.Monitoring.Reporters;
using Orc.Monitoring.Reporters.ReportOutputs;


[TestFixture]
public class PerformanceMonitorIntegrationTests
{
    private static TestReporter _testReporter;

    private class TestReporter : IMethodCallReporter
    {
        public int CallCount { get; private set; }
        public string Name => "TestReporter";
        public string FullName => "TestReporter";
        public MethodInfo? RootMethod { get; set; }

        public IAsyncDisposable StartReporting(IObservable<ICallStackItem> callStack)
        {
            CallCount++;
            Console.WriteLine($"TestReporter.StartReporting called. CallCount: {CallCount}");

            var subscription = callStack.Subscribe(
                onNext: item => Console.WriteLine($"TestReporter received item: {item}"),
                onError: ex => Console.WriteLine($"TestReporter error: {ex}"),
                onCompleted: () => Console.WriteLine("TestReporter completed")
            );

            return new AsyncDisposable(async () =>
            {
                Console.WriteLine("TestReporter.StartReporting disposing");
                subscription.Dispose();
            });
        }

        public IOutputContainer AddOutput<TOutput>(object? parameter = null) where TOutput : IReportOutput, new()
        {
            Console.WriteLine("TestReporter.AddOutput called");
            return this;
        }

        public void Reset()
        {
            CallCount = 0;
            Console.WriteLine("TestReporter.Reset called");
        }
    }

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
                Reporters = new List<IMethodCallReporter> { _testReporter }
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
                Reporters = new List<IMethodCallReporter> { _testReporter }
            });
            Console.WriteLine("TestClass.TestAsyncMethod executing");
            await Task.Delay(10);
            Console.WriteLine("TestClass.TestAsyncMethod exited");
        }
    }

    [SetUp]
    public void Setup()
    {
        Console.WriteLine("Setup started");
        MonitoringController.ResetForTesting();  // Add this line to reset the state
        PerformanceMonitor.Configure(builder => {
            Console.WriteLine($"Configuring assembly: {typeof(TestClass).Assembly.FullName}");
            builder.TrackAssembly(typeof(TestClass).Assembly);
        });
        PerformanceMonitor.AddTrackedMethod(typeof(TestClass), typeof(TestClass).GetMethod("TestMethod")!);
        PerformanceMonitor.AddTrackedMethod(typeof(TestClass), typeof(TestClass).GetMethod("TestAsyncMethod")!);
        MonitoringController.Enable();
        Console.WriteLine($"Monitoring enabled: {MonitoringController.IsEnabled}");
        _testReporter = new TestReporter();
        MonitoringController.EnableReporter(typeof(TestReporter));  // Add this line to enable the TestReporter
        Console.WriteLine("Setup completed");

        // Force re-creation of TestClass monitor
        typeof(TestClass).TypeInitializer?.Invoke(null, null);
    }

    [Test]
    public void WhenMonitoringIsEnabled_MethodsAreTracked()
    {
        var testClass = new TestClass();
        testClass.TestMethod();
        Assert.That(_testReporter.CallCount, Is.EqualTo(1), "Expected 1 call for the sync method");
    }

    [Test]
    public async Task WhenMonitoringIsEnabled_AsyncMethodsAreTracked()
    {
        Console.WriteLine("Async test started");
        var testClass = new TestClass();
        await testClass.TestAsyncMethod();
        Console.WriteLine($"Final CallCount: {_testReporter.CallCount}");
        Assert.That(_testReporter.CallCount, Is.EqualTo(1), "Expected 1 call for the async method");
    }

    [Test]
    public void WhenMonitoringIsDisabled_MethodsAreNotTracked()
    {
        MonitoringController.Disable();
        var testClass = new TestClass();
        testClass.TestMethod();
        Assert.That(_testReporter.CallCount, Is.EqualTo(0), "Expected no calls when monitoring is disabled");
    }

    [Test]
    public async Task WhenMonitoringIsDisabled_AsyncMethodsAreNotTracked()
    {
        MonitoringController.Disable();
        var testClass = new TestClass();
        await testClass.TestAsyncMethod();
        Assert.That(_testReporter.CallCount, Is.EqualTo(0), "Expected no calls when monitoring is disabled");
    }

    [Test]
    public void WhenMonitoringIsToggled_TrackingRespondsAccordingly()
    {
        var testClass = new TestClass();
        testClass.TestMethod();
        Assert.That(_testReporter.CallCount, Is.EqualTo(1), "Expected 1 call when monitoring is enabled");

        MonitoringController.Disable();
        testClass.TestMethod();
        Assert.That(_testReporter.CallCount, Is.EqualTo(1), "Expected no additional calls when monitoring is disabled");

        MonitoringController.Enable();
        testClass.TestMethod();
        Assert.That(_testReporter.CallCount, Is.EqualTo(2), "Expected 1 additional call when monitoring is re-enabled");
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

        Console.WriteLine($"Final CallCount: {_testReporter.CallCount}");
        Assert.That(_testReporter.CallCount, Is.EqualTo(2), "Expected 2 calls: 1 before disabling, 1 after disabling");
    }

    [Test]
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

        Console.WriteLine($"Final CallCount: {_testReporter.CallCount}");
        Assert.That(_testReporter.CallCount, Is.EqualTo(1), "Expected 1 call: method started before disabling should complete");
    }
}

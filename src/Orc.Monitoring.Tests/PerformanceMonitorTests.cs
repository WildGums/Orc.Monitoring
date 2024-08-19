namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using System;
using System.Reflection;
using Moq;
using Orc.Monitoring.Reporters;
using Orc.Monitoring.Filters;
using Orc.Monitoring.Reporters.ReportOutputs;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

[TestFixture]
public class PerformanceMonitorTests
{
    private MockReporter _mockReporter;
    private ILogger<PerformanceMonitorTests> _logger;

    [SetUp]
    public void Setup()
    {
        _logger = MonitoringController.CreateLogger<PerformanceMonitorTests>();
        PerformanceMonitor.Reset();
        MonitoringController.ResetForTesting();
        _mockReporter = new MockReporter();
        _logger.LogInformation("Test setup complete");
    }

    [Test]
    public void Configure_ShouldCreateSingleCallStackInstance()
    {
        _logger.LogInformation("Starting Configure_ShouldCreateSingleCallStackInstance test");

        // Step 1: Check initial state
        _logger.LogDebug("Step 1: Checking initial state");
        Assert.That(PerformanceMonitor.IsConfigured, Is.False, "PerformanceMonitor should not be configured initially");
        Assert.That(GetCallStackInstance(), Is.Null, "CallStack should be null initially");

        // Step 2: Configure PerformanceMonitor
        _logger.LogDebug("Step 2: Configuring PerformanceMonitor");
        PerformanceMonitor.Configure(config =>
        {
            _logger.LogDebug("Inside configuration action");
            config.AddReporter(_mockReporter.GetType());
        });

        // Step 3: Check post-configuration state
        _logger.LogDebug("Step 3: Checking post-configuration state");
        Assert.That(PerformanceMonitor.IsConfigured, Is.True, "PerformanceMonitor should be configured after Configure");
        var callStackAfterConfigure = GetCallStackInstance();
        _logger.LogDebug($"CallStack after Configure: {callStackAfterConfigure}");
        Assert.That(callStackAfterConfigure, Is.Not.Null, "CallStack should be created during configuration");

        // Step 4: Create monitors
        _logger.LogDebug("Step 4: Creating monitors");
        var monitor1 = PerformanceMonitor.ForClass<PerformanceMonitorTests>();
        _logger.LogDebug($"Monitor1 type: {monitor1.GetType().Name}");
        var monitor2 = PerformanceMonitor.ForClass<PerformanceMonitorTests>();
        _logger.LogDebug($"Monitor2 type: {monitor2.GetType().Name}");

        // Step 5: Final check
        _logger.LogDebug("Step 5: Final check");
        var finalCallStack = GetCallStackInstance();
        _logger.LogDebug($"Final CallStack: {finalCallStack}");
        Assert.That(finalCallStack, Is.Not.Null, "CallStack should be accessible after creating monitors");
        Assert.That(finalCallStack, Is.SameAs(callStackAfterConfigure), "CallStack instance should remain the same");

        _logger.LogInformation("Configure_ShouldCreateSingleCallStackInstance test completed");
    }

    [Test]
    public void ForClass_WhenNotConfigured_ShouldReturnNullClassMonitor()
    {
        var monitor = PerformanceMonitor.ForClass<PerformanceMonitorTests>();
        Assert.That(monitor, Is.InstanceOf<NullClassMonitor>());
    }

    [Test]
    public void ForClass_WhenConfigured_ShouldReturnClassMonitor()
    {
        PerformanceMonitor.Configure(config => { config.AddReporter(_mockReporter.GetType()); });
        var monitor = PerformanceMonitor.ForClass<PerformanceMonitorTests>();
        Assert.That(monitor, Is.InstanceOf<ClassMonitor>());
    }

    [Test]
    public void Reset_ShouldClearConfigurationAndCallStack()
    {
        PerformanceMonitor.Configure(config => { config.AddReporter(_mockReporter.GetType()); });
        Assert.That(PerformanceMonitor.IsConfigured, Is.True);

        PerformanceMonitor.Reset();

        Assert.That(PerformanceMonitor.IsConfigured, Is.False);
        Assert.That(PerformanceMonitor.GetCurrentConfiguration(), Is.Null);
        Assert.That(GetCallStackInstance(), Is.Null);
    }

    [Test]
    public void Configure_ShouldEnableMonitoring()
    {
        MonitoringController.Disable();
        PerformanceMonitor.Configure(config => { config.AddReporter(_mockReporter.GetType()); });
        Assert.That(MonitoringController.IsEnabled, Is.True);
    }

    [Test]
    public void Configure_ShouldEnableDefaultOutputTypes()
    {
        PerformanceMonitor.Configure(config => { });
        Assert.That(MonitoringController.IsOutputTypeEnabled<RanttOutput>(), Is.True);
        Assert.That(MonitoringController.IsOutputTypeEnabled<TxtReportOutput>(), Is.True);
    }

    [Test]
    public void Configure_WithCustomConfiguration_ShouldApplyConfiguration()
    {
        var mockFilter = new Mock<IMethodFilter>();

        PerformanceMonitor.Configure(config =>
        {
            config.AddReporter(_mockReporter.GetType());
            config.AddFilter<AlwaysIncludeFilter>();
        });

        var configuration = PerformanceMonitor.GetCurrentConfiguration();
        Assert.That(configuration, Is.Not.Null);
        Assert.That(MonitoringController.IsReporterEnabled(_mockReporter.GetType()), Is.True);
    }

    [Test]
    public void ForCurrentClass_ShouldReturnClassMonitorForCallingType()
    {
        PerformanceMonitor.Configure(config => { config.AddReporter(_mockReporter.GetType()); });
        var monitor = PerformanceMonitor.ForCurrentClass();
        Assert.That(monitor, Is.InstanceOf<ClassMonitor>());
    }

    [Test]
    public void Configure_WithMultipleReporters_ShouldEnableAllReporters()
    {
        var secondMockReporter = new MockReporter();

        PerformanceMonitor.Configure(config =>
        {
            config.AddReporter(_mockReporter.GetType());
            config.AddReporter(secondMockReporter.GetType());
        });

        Assert.That(MonitoringController.IsReporterEnabled(_mockReporter.GetType()), Is.True);
        Assert.That(MonitoringController.IsReporterEnabled(secondMockReporter.GetType()), Is.True);
    }

    [Test]
    public void Configure_WithCustomOutputTypeState_ShouldApplyState()
    {
        PerformanceMonitor.Configure(config =>
        {
            config.SetOutputTypeState<CsvReportOutput>(true);
            config.SetOutputTypeState<RanttOutput>(false);
        });

        Assert.That(MonitoringController.IsOutputTypeEnabled<CsvReportOutput>(), Is.True);
        Assert.That(MonitoringController.IsOutputTypeEnabled<RanttOutput>(), Is.False);
    }

    [Test]
    public async Task Configure_ShouldHandleConcurrentAccessAsync()
    {
        var configTask1 = Task.Run(() => PerformanceMonitor.Configure(config => { config.AddReporter(_mockReporter.GetType()); }));
        var configTask2 = Task.Run(() => PerformanceMonitor.Configure(config => { config.AddReporter(_mockReporter.GetType()); }));

        await Task.WhenAll(configTask1, configTask2);

        Assert.That(PerformanceMonitor.IsConfigured, Is.True);
        Assert.That(MonitoringController.IsReporterEnabled(_mockReporter.GetType()), Is.True);
    }

    [Test]
    public void GetCurrentConfiguration_AfterConfigure_ShouldReturnNonNullConfiguration()
    {
        PerformanceMonitor.Configure(config => { config.AddReporter(_mockReporter.GetType()); });
        var configuration = PerformanceMonitor.GetCurrentConfiguration();
        Assert.That(configuration, Is.Not.Null);
    }

    [Test]
    public void IsConfigured_BeforeAndAfterConfigure_ShouldReturnCorrectValue()
    {
        Assert.That(PerformanceMonitor.IsConfigured, Is.False, "Should not be configured initially");

        PerformanceMonitor.Configure(config => { config.AddReporter(_mockReporter.GetType()); });

        Assert.That(PerformanceMonitor.IsConfigured, Is.True, "Should be configured after Configure is called");
    }

    private CallStack? GetCallStackInstance()
    {
        var field = typeof(PerformanceMonitor).GetField("_callStack", BindingFlags.NonPublic | BindingFlags.Static);
        var callStack = field?.GetValue(null) as CallStack;
        _logger.LogDebug($"GetCallStackInstance returned: {callStack}");
        return callStack;
    }
}

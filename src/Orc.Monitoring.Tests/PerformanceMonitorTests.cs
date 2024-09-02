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
    private TestLogger<PerformanceMonitorTests> _logger;
    private TestLoggerFactory<PerformanceMonitorTests> _loggerFactory;
    private IMonitoringController _monitoringController;
    private IPerformanceMonitor _performanceMonitor;
    private IClassMonitorFactory _classMonitorFactory;

    [SetUp]
    public void Setup()
    {
        _logger = new TestLogger<PerformanceMonitorTests>();
        _loggerFactory = new TestLoggerFactory<PerformanceMonitorTests>(_logger);

        _monitoringController = new MonitoringController(_loggerFactory, () => new EnhancedDataPostProcessor(_loggerFactory));
        var methodCallInfoPool = new MethodCallInfoPool(_monitoringController, _loggerFactory);
        var methodCallContextFactory = new MethodCallContextFactory(_monitoringController, _loggerFactory, methodCallInfoPool);
        _classMonitorFactory = new ClassMonitorFactory(_monitoringController, _loggerFactory, methodCallContextFactory, methodCallInfoPool);

        _performanceMonitor = new PerformanceMonitor(_monitoringController, _loggerFactory,
            (config) => new CallStack(_monitoringController, config, methodCallInfoPool, _loggerFactory),
            _classMonitorFactory,
            () => new ConfigurationBuilder(_monitoringController));

        _performanceMonitor.Reset();
        _mockReporter = new MockReporter(_loggerFactory);
        _logger.LogInformation("Test setup complete");

        _monitoringController.Enable();
    }

    [Test]
    public void Configure_ShouldCreateSingleCallStackInstance()
    {
        _logger.LogInformation("Starting Configure_ShouldCreateSingleCallStackInstance test");

        // Step 1: Check initial state
        _logger.LogDebug("Step 1: Checking initial state");
        Assert.That(_performanceMonitor.IsConfigured, Is.False, "PerformanceMonitor should not be configured initially");
        Assert.That(GetCallStackInstance(), Is.Null, "CallStack should be null initially");

        // Step 2: Configure PerformanceMonitor
        _logger.LogDebug("Step 2: Configuring PerformanceMonitor");
        _performanceMonitor.Configure(config =>
        {
            _logger.LogDebug("Inside configuration action");
            config.AddReporterType(_mockReporter.GetType());
        });

        // Step 3: Check post-configuration state
        _logger.LogDebug("Step 3: Checking post-configuration state");
        Assert.That(_performanceMonitor.IsConfigured, Is.True, "PerformanceMonitor should be configured after Configure");
        var callStackAfterConfigure = GetCallStackInstance();
        _logger.LogDebug($"CallStack after Configure: {callStackAfterConfigure}");
        Assert.That(callStackAfterConfigure, Is.Not.Null, "CallStack should be created during configuration");

        // Step 4: Create monitors
        _logger.LogDebug("Step 4: Creating monitors");
        var monitor1 = _performanceMonitor.ForClass<PerformanceMonitorTests>();
        _logger.LogDebug($"Monitor1 type: {monitor1.GetType().Name}");
        var monitor2 = _performanceMonitor.ForClass<PerformanceMonitorTests>();
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
        var monitor = _performanceMonitor.ForClass<PerformanceMonitorTests>();
        Assert.That(monitor, Is.InstanceOf<NullClassMonitor>());
    }

    [Test]
    public void ForClass_WhenConfigured_ShouldReturnClassMonitor()
    {
        _performanceMonitor.Configure(config => { config.AddReporterType(_mockReporter.GetType()); });
        var monitor = _performanceMonitor.ForClass<PerformanceMonitorTests>();
        Assert.That(monitor, Is.InstanceOf<ClassMonitor>());
    }

    [Test]
    public void Reset_ShouldClearConfigurationAndCallStack()
    {
        _performanceMonitor.Configure(config => { config.AddReporterType(_mockReporter.GetType()); });
        Assert.That(_performanceMonitor.IsConfigured, Is.True);

        _performanceMonitor.Reset();

        Assert.That(_performanceMonitor.IsConfigured, Is.False);
        Assert.That(_performanceMonitor.GetCurrentConfiguration(), Is.Null);
        Assert.That(GetCallStackInstance(), Is.Null);
    }

    [Test]
    public void Configure_ShouldEnableMonitoring()
    {
        _monitoringController.Disable();
        _performanceMonitor.Configure(config => { config.AddReporterType(_mockReporter.GetType()); });
        Assert.That(_monitoringController.IsEnabled, Is.True);
    }

    [Test]
    public void Configure_ShouldEnableDefaultOutputTypes()
    {
        _performanceMonitor.Configure(config => { });
        Assert.That(_monitoringController.IsOutputTypeEnabled<RanttOutput>(), Is.True);
        Assert.That(_monitoringController.IsOutputTypeEnabled<TxtReportOutput>(), Is.True);
    }

    [Test]
    public void Configure_WithCustomConfiguration_ShouldApplyConfiguration()
    {
        var mockFilter = new AlwaysIncludeFilter(_loggerFactory);

        _performanceMonitor.Configure(config =>
        {
            config.AddReporterType(_mockReporter.GetType());
            config.AddFilter(mockFilter);
        });

        var configuration = _performanceMonitor.GetCurrentConfiguration();
        Assert.That(configuration, Is.Not.Null);
        Assert.That(_monitoringController.IsReporterEnabled(_mockReporter.GetType()), Is.True);
        Assert.That(_monitoringController.IsFilterEnabled(mockFilter.GetType()), Is.True);
    }

    [Test]
    public void ForCurrentClass_ShouldReturnClassMonitorForCallingType()
    {
        _performanceMonitor.Configure(config => { config.AddReporterType(_mockReporter.GetType()); });
        var monitor = _performanceMonitor.ForCurrentClass();
        Assert.That(monitor, Is.InstanceOf<ClassMonitor>());
    }

    [Test]
    public void Configure_WithMultipleReporters_ShouldEnableAllReporters()
    {
        var secondMockReporter = new MockReporter(_loggerFactory);

        _performanceMonitor.Configure(config =>
        {
            config.AddReporterType(_mockReporter.GetType());
            config.AddReporterType(secondMockReporter.GetType());
        });

        Assert.That(_monitoringController.IsReporterEnabled(_mockReporter.GetType()), Is.True);
        Assert.That(_monitoringController.IsReporterEnabled(secondMockReporter.GetType()), Is.True);
    }

    [Test]
    public void Configure_WithCustomOutputTypeState_ShouldApplyState()
    {
        _performanceMonitor.Configure(config =>
        {
            config.SetOutputTypeState<CsvReportOutput>(true);
            config.SetOutputTypeState<RanttOutput>(false);
        });

        Assert.That(_monitoringController.IsOutputTypeEnabled<CsvReportOutput>(), Is.True);
        Assert.That(_monitoringController.IsOutputTypeEnabled<RanttOutput>(), Is.False);
    }

    [Test]
    public async Task Configure_ShouldHandleConcurrentAccessAsync()
    {
        var configTask1 = Task.Run(() => _performanceMonitor.Configure(config => { config.AddReporterType(_mockReporter.GetType()); }));
        var configTask2 = Task.Run(() => _performanceMonitor.Configure(config => { config.AddReporterType(_mockReporter.GetType()); }));

        await Task.WhenAll(configTask1, configTask2);

        Assert.That(_performanceMonitor.IsConfigured, Is.True);
        Assert.That(_monitoringController.IsReporterEnabled(_mockReporter.GetType()), Is.True);
    }

    [Test]
    public void GetCurrentConfiguration_AfterConfigure_ShouldReturnNonNullConfiguration()
    {
        _performanceMonitor.Configure(config => { config.AddReporterType(_mockReporter.GetType()); });
        var configuration = _performanceMonitor.GetCurrentConfiguration();
        Assert.That(configuration, Is.Not.Null);
    }

    [Test]
    public void IsConfigured_BeforeAndAfterConfigure_ShouldReturnCorrectValue()
    {
        Assert.That(_performanceMonitor.IsConfigured, Is.False, "Should not be configured initially");

        _performanceMonitor.Configure(config => { config.AddReporterType(_mockReporter.GetType()); });

        Assert.That(_performanceMonitor.IsConfigured, Is.True, "Should be configured after Configure is called");
    }

    [Test]
    public void Configure_WithAssemblyTracking_ShouldTrackAssembly()
    {
        var assembly = typeof(PerformanceMonitorTests).Assembly;

        _performanceMonitor.Configure(config =>
        {
            config.TrackAssembly(assembly);
        });

        var configuration = _performanceMonitor.GetCurrentConfiguration();
        Assert.That(configuration, Is.Not.Null);
        Assert.That(configuration.TrackedAssemblies, Does.Contain(assembly));
    }

    [Test]
    public void Configure_WithMultipleFilters_ShouldAddAllFilters()
    {
        var filter1 = new AlwaysIncludeFilter(_loggerFactory);
        var filter2 = new WorkflowItemFilter();

        _performanceMonitor.Configure(config =>
        {
            config.AddFilter(filter1);
            config.AddFilter(filter2);
        });

        var configuration = _performanceMonitor.GetCurrentConfiguration();
        Assert.That(configuration, Is.Not.Null);
        Assert.That(configuration.Filters, Does.Contain(filter1));
        Assert.That(configuration.Filters, Does.Contain(filter2));
    }

    [Test]
    public void Configure_WithGlobalState_ShouldSetGlobalState()
    {
        _performanceMonitor.Configure(config =>
        {
            config.SetGlobalState(false);
        });

        Assert.That(_monitoringController.IsEnabled, Is.False);

        _performanceMonitor.Configure(config =>
        {
            config.SetGlobalState(true);
        });

        Assert.That(_monitoringController.IsEnabled, Is.True);
    }

    private CallStack? GetCallStackInstance()
    {
        var field = typeof(PerformanceMonitor).GetField("_callStack", BindingFlags.NonPublic | BindingFlags.Static);
        var callStack = field?.GetValue(null) as CallStack;
        _logger.LogDebug($"GetCallStackInstance returned: {callStack}");
        return callStack;
    }
}

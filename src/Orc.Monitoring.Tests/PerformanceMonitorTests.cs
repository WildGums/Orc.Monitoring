namespace Orc.Monitoring.Tests;

using System.Linq;
using NUnit.Framework;
using System.Reflection;
using Filters;
using Reporters.ReportOutputs;
using System.Threading.Tasks;
using Castle.Core.Internal;
using Core.Abstractions;
using Core.CallStacks;
using Core.Configuration;
using Core.Controllers;
using Core.MethodCallContexts;
using Core.Monitors;
using Core.PerformanceMonitoring;
using Core.Pooling;
using Microsoft.Extensions.Logging;
using TestUtilities.Filters;
using TestUtilities.Logging;
using TestUtilities.Mocks;

[TestFixture]
public class PerformanceMonitorTests
{
    private MockReporter _mockReporter;
    private TestLogger<PerformanceMonitorTests> _logger;
    private TestLoggerFactory<PerformanceMonitorTests> _loggerFactory;
    private IMonitoringController _monitoringController;
    private IPerformanceMonitor _performanceMonitor;
    private IClassMonitorFactory _classMonitorFactory;
    private MethodCallInfoPool _methodCallInfoPool;
    private MethodCallContextFactory _methodCallContextFactory;

    [SetUp]
    public void Setup()
    {
        _logger = new TestLogger<PerformanceMonitorTests>();
        _loggerFactory = new TestLoggerFactory<PerformanceMonitorTests>(_logger);
        _monitoringController = new MonitoringController(_loggerFactory);
        _methodCallInfoPool = new MethodCallInfoPool(_monitoringController, _loggerFactory);
        _methodCallContextFactory = new MethodCallContextFactory(_monitoringController, _loggerFactory, _methodCallInfoPool);

        _classMonitorFactory = new ClassMonitorFactory(_monitoringController, _loggerFactory, _methodCallContextFactory, _methodCallInfoPool);

        var callStackFactory = new CallStackFactory(_monitoringController, _loggerFactory, _methodCallInfoPool);

        _performanceMonitor = new PerformanceMonitor(
            _monitoringController,
            _loggerFactory,
            callStackFactory,
            _classMonitorFactory);

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
        // Skipped after refactoring

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
        var monitor = _performanceMonitor.ForClass<PerformanceMonitorTests>();
        Assert.That(monitor, Is.InstanceOf<ClassMonitor>());
    }

    [Test]
    public void Reset_ShouldClearCallStack()
    {
        Assert.That(_performanceMonitor.IsConfigured, Is.True);

        _performanceMonitor.Reset();

        Assert.That(_performanceMonitor.IsConfigured, Is.False);
        Assert.That(GetCallStackInstance(), Is.Null);
    }

    [Test]
    public void ForCurrentClass_ShouldReturnClassMonitorForCallingType()
    {
        var monitor = _performanceMonitor.ForCurrentClass();
        Assert.That(monitor, Is.InstanceOf<ClassMonitor>());
    }


    private CallStack? GetCallStackInstance()
    {
        var field = typeof(PerformanceMonitor).GetField("_callStack", BindingFlags.NonPublic | BindingFlags.Instance);
        var callStack = field?.GetValue(_performanceMonitor) as CallStack;
        _logger.LogDebug($"GetCallStackInstance returned: {callStack?.GetType().Name ?? "null"}");
        return callStack;
    }
}

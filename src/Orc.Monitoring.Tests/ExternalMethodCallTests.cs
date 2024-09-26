#pragma warning disable CL0002
namespace Orc.Monitoring.Tests;

using System;
using System.Threading.Tasks;
using Core.Abstractions;
using Core.CallStacks;
using Core.Configuration;
using Core.Controllers;
using Core.MethodCallContexts;
using Core.Models;
using Core.Monitors;
using Core.Pooling;
using NUnit.Framework;
using Moq;
using Monitoring;
using Filters;
using Microsoft.Extensions.Logging;
using TestUtilities.Logging;
using TestUtilities.Mocks;

[TestFixture]
public class ExternalMethodCallTests
{
    private MockReporter _mockReporter;
    private Mock<IMethodFilter> _mockFilter;
    private MonitoringConfiguration _config;
    private CallStack _callStack;
    private MethodCallInfoPool _methodCallInfoPool;
    private TestLogger<ExternalMethodCallTests> _logger;
    private TestLoggerFactory<ExternalMethodCallTests> _loggerFactory;
    private IMonitoringController _monitoringController;
    private MethodCallContextFactory _methodCallContextFactory;
    private ClassMonitor _monitor;

    [SetUp]
    public void Setup()
    {
        _logger = new TestLogger<ExternalMethodCallTests>();
        _loggerFactory = new TestLoggerFactory<ExternalMethodCallTests>(_logger);
        _loggerFactory.EnableLoggingFor<MethodCallInfoPool>();
        _loggerFactory.EnableLoggingFor<CallStack>();
        _loggerFactory.EnableLoggingFor<ClassMonitor>();

        _monitoringController = new MonitoringController(_loggerFactory);

        _logger.LogInformation("Setup started");

        _mockReporter = new MockReporter(_loggerFactory);
        _mockFilter = new Mock<IMethodFilter>();
        _config = new MonitoringConfiguration();
        _methodCallInfoPool = new MethodCallInfoPool(_monitoringController, _loggerFactory);
        _callStack = new CallStack(_monitoringController, _config, _methodCallInfoPool, _loggerFactory);
        _methodCallContextFactory = new MethodCallContextFactory(_monitoringController, _loggerFactory, _methodCallInfoPool);

        _config.RegisterComponentType(_mockReporter.GetType());
        _config.AddComponentInstance(_mockFilter.Object);

        _monitoringController.Configuration = _config;
        _monitoringController.Enable();
        _monitoringController.EnableReporter(_mockReporter.GetType());

        _mockFilter.Setup(f => f.ShouldInclude(It.IsAny<MethodCallInfo>())).Returns(true);

        _monitor = new ClassMonitor(_monitoringController, typeof(ExternalDependency), _callStack, _config, _loggerFactory, _methodCallContextFactory, _methodCallInfoPool);

        _logger.LogInformation($"Initial setup - IsEnabled: {_monitoringController.IsEnabled}, MockReporter enabled: {_monitoringController.IsReporterEnabled(_mockReporter.GetType())}");
    }

    [Test]
    public void ExternalMethodCall_IsCorrectlyMonitored()
    {
        _logger.LogInformation("Starting ExternalMethodCall_IsCorrectlyMonitored test");

        using (var _ = _monitor.StartMethod(new MethodConfiguration
        {
            Reporters = [_mockReporter]
        }, nameof(ExternalDependency.SimpleMethod)))
        {
            ExternalDependency.SimpleMethod();
        }

        Assert.That(_mockReporter.StartReportingCallCount, Is.EqualTo(1), "StartReporting should be called once for external method");
        Assert.That(_mockReporter.OperationSequence, Does.Contain("SetRootMethod"), "SetRootMethod should be called");
        _logger.LogInformation($"ExternalMethodCall_IsCorrectlyMonitored completed. StartReporting called: {_mockReporter.StartReportingCallCount}");
    }

    [Test]
    public async Task AsyncExternalMethodCall_IsCorrectlyMonitored()
    {
        _logger.LogInformation("Starting AsyncExternalMethodCall_IsCorrectlyMonitored test");

        var completionSource = new TaskCompletionSource<bool>();
        _mockReporter.OnStartReporting = _ => completionSource.SetResult(true);

        await using (var _ = _monitor.StartAsyncMethod(new MethodConfiguration
        {
            Reporters = [_mockReporter]
        }, nameof(ExternalDependency.AsyncMethodAsync)))
        {
            await ExternalDependency.AsyncMethodAsync();
        }

        await Task.WhenAny(completionSource.Task, Task.Delay(5000)); // 5 second timeout

        Assert.That(_mockReporter.StartReportingCallCount, Is.EqualTo(1), "StartReporting should be called once for async external method");
        _logger.LogInformation($"AsyncExternalMethodCall_IsCorrectlyMonitored completed. StartReporting called: {_mockReporter.StartReportingCallCount}");
    }

    [Test]
    public void ExternalMethodCall_WithException_IsHandledCorrectly()
    {
        _logger.LogInformation("Starting ExternalMethodCall_WithException_IsHandledCorrectly test");

        Assert.Throws<InvalidOperationException>(() =>
        {
            using (var context = _monitor.StartMethod(new MethodConfiguration
            {
                Reporters = [_mockReporter]
            }, nameof(ExternalDependency.MethodWithException)))
            {
                ExternalDependency.MethodWithException();
            }
        });

        Assert.That(_mockReporter.StartReportingCallCount, Is.EqualTo(1), "StartReporting should be called once for external method with exception");
        _logger.LogInformation($"ExternalMethodCall_WithException_IsHandledCorrectly completed. StartReporting called: {_mockReporter.StartReportingCallCount}");
    }
}

// Simulated external dependency
public static class ExternalDependency
{
    public static void SimpleMethod() { }
    public static async Task AsyncMethodAsync() { await Task.Delay(1); }
    public static void MethodWithException() { throw new InvalidOperationException("Test exception"); }
    public static void MethodWithParameters(int param1, string param2) { }
    public static void NestedMethod() { SimpleMethod(); }
}

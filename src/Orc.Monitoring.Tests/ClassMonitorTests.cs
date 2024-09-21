#pragma warning disable CL0002
namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using Moq;
using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;
using Core.Abstractions;
using Core.CallStacks;
using Core.Configuration;
using Core.Controllers;
using Core.MethodCallContexts;
using Core.MethodLifecycle;
using Core.Models;
using Core.Monitors;
using Core.Pooling;
using TestUtilities.Logging;

[TestFixture]
public class ClassMonitorTests
{
    private IMonitoringController _controller;
    private CallStack _callStack;
    private TestLoggerFactory<ClassMonitorTests> _loggerFactory;
    private TestLogger<ClassMonitorTests> _logger;
    private IMethodCallContextFactory _contextFactory;
    private MethodCallInfoPool _methodCallInfoPool;
    private MonitoringConfiguration _config;
    private ClassMonitor _classMonitor;

    [SetUp]
    public void Setup()
    {
        _logger = new TestLogger<ClassMonitorTests>();
        _loggerFactory = new TestLoggerFactory<ClassMonitorTests>(_logger);
        _controller = new MonitoringController(_loggerFactory);

        _methodCallInfoPool = new MethodCallInfoPool(_controller, _loggerFactory);
        _contextFactory = new MethodCallContextFactory(_controller, _loggerFactory, _methodCallInfoPool);
        _config = new MonitoringConfiguration();
        _callStack = new CallStack(_controller, _config, _methodCallInfoPool, _loggerFactory);
        _classMonitor = new ClassMonitor(_controller, typeof(ClassMonitorTests), _callStack, _config, _loggerFactory, _contextFactory, _methodCallInfoPool);

        _controller.Enable();
    }

    [Test]
    public void StartMethod_WhenMonitoringEnabled_ReturnsMethodCallContext()
    {
        // Arrange
        var config = new MethodConfiguration();
        _controller.Enable();

        // Act
        var result = _classMonitor.StartMethod(config);
#pragma warning disable IDISP016
#pragma warning disable IDISP017
        result.Dispose();
#pragma warning restore IDISP016
#pragma warning restore IDISP017

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.AssignableTo<MethodCallContext>());
    }

    [Test]
    public void StartMethod_WhenMonitoringDisabled_ReturnsDummyContext()
    {
        // Arrange
        var config = new MethodConfiguration();
        _controller.Disable();

        // Act
        var result = _classMonitor.StartMethod(config);
#pragma warning disable IDISP016
#pragma warning disable IDISP017
        result.Dispose();
#pragma warning restore IDISP016
#pragma warning restore IDISP017

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.AssignableTo<NullMethodCallContext>());
    }

    [Test]
    public async Task StartAsyncMethod_WhenMonitoringEnabled_ReturnsAsyncMethodCallContext()
    {
        // Arrange
        var config = new MethodConfiguration();
        _controller.Enable();

        // Act
        var result = _classMonitor.StartAsyncMethod(config);
#pragma warning disable IDISP016
        await result.DisposeAsync();
#pragma warning restore IDISP016

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.AssignableTo<AsyncMethodCallContext>());
    }

    [Test]
    public void LogStatus_WhenMonitoringEnabled_LogsStatus()
    {
        // Arrange
        var mockStatus = new Mock<IMethodLifeCycleItem>();
        mockStatus.SetupGet(s => s.MethodCallInfo).Returns(new MethodCallInfo { MethodInfo = new Mock<MethodInfo>().Object });

        _controller.Enable();

        var items = new List<IMethodLifeCycleItem>();
        using var _ = _callStack.Subscribe(x => items.Add((IMethodLifeCycleItem)x));

        // Act
        _classMonitor.LogStatus(mockStatus.Object);

        // Assert
        Assert.That(items, Has.Count.EqualTo(1));
        Assert.That(items[0], Is.EqualTo(mockStatus.Object));
    }

    [Test]
    public void LogStatus_WhenMonitoringDisabled_DoesNotLogStatus()
    {
        // Arrange
        var mockStatus = new Mock<IMethodLifeCycleItem>();
        mockStatus.SetupGet(s => s.MethodCallInfo).Returns(new MethodCallInfo { MethodInfo = new Mock<MethodInfo>().Object });

        _controller.Disable();

        var items = new List<IMethodLifeCycleItem>();
        using var _ = _callStack.Subscribe(x => items.Add((IMethodLifeCycleItem)x));

        // Act
        _classMonitor.LogStatus(mockStatus.Object);

        // Assert
        Assert.That(items, Has.Count.EqualTo(0));
    }

    [Test]
    public void StartExternalMethod_WhenMonitoringEnabled_ReturnsMethodCallContext()
    {
        // Arrange
        var config = new MethodConfiguration();
        _controller.Enable();

        // Act
        var result = _classMonitor.StartExternalMethod(config, typeof(string), nameof(string.Clone));
#pragma warning disable IDISP016
#pragma warning disable IDISP017
        result.Dispose();
#pragma warning restore IDISP016
#pragma warning restore IDISP017

        // Assert
        Assert.That(result, Is.Not.Null);
    }
}

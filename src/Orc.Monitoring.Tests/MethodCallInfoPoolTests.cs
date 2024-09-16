namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using Moq;
using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Orc.Monitoring.Reporters;


[TestFixture]
public class MethodCallInfoPoolTests
{
    private MethodCallInfoPool _pool;
    private Mock<IMonitoringController> _mockController;
    private Mock<IMonitoringLoggerFactory> _mockLoggerFactory;
    private Mock<ILogger<MethodCallInfoPool>> _mockLogger;
    private Mock<IClassMonitor> _mockClassMonitor;
    private MethodInfo _testMethod;

    [SetUp]
    public void Setup()
    {
        _mockController = new Mock<IMonitoringController>();
        _mockLoggerFactory = new Mock<IMonitoringLoggerFactory>();
        _mockLogger = new Mock<ILogger<MethodCallInfoPool>>();
        _mockClassMonitor = new Mock<IClassMonitor>();

        _mockLoggerFactory.Setup(f => f.CreateLogger<MethodCallInfoPool>()).Returns(_mockLogger.Object);
        _mockController.Setup(c => c.IsEnabled).Returns(true);

        _pool = new MethodCallInfoPool(_mockController.Object, _mockLoggerFactory.Object);

        // Use a method that doesn't have overloads to avoid ambiguity
        _testMethod = typeof(string).GetMethod("Substring", new[] { typeof(int) });
    }

    [Test]
    public void Rent_WhenMonitoringEnabled_ReturnsMethodCallInfo()
    {
        // Arrange
        var genericArguments = new Type[0];
        var attributeParameters = new Dictionary<string, string>();

        // Act
        var result = _pool.Rent(_mockClassMonitor.Object, typeof(string), _testMethod, genericArguments, "testId", attributeParameters);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.IsNull, Is.False);
        Assert.That(result.MethodInfo, Is.EqualTo(_testMethod));
    }

    [Test]
    public void Rent_WhenMonitoringDisabled_ReturnsNullMethodCallInfo()
    {
        // Arrange
        _mockController.Setup(c => c.IsEnabled).Returns(false);
        var genericArguments = new Type[0];
        var attributeParameters = new Dictionary<string, string>();

        // Act
        var result = _pool.Rent(_mockClassMonitor.Object, typeof(string), _testMethod, genericArguments, "testId", attributeParameters);

        // Assert
        Assert.That(result.IsNull, Is.True);
    }

    [Test]
    public void Rent_WithExternalCall_SetsExternalCallProperties()
    {
        // Arrange
        var genericArguments = new Type[0];
        var attributeParameters = new Dictionary<string, string>();

        // Act
        var result = _pool.Rent(_mockClassMonitor.Object, typeof(string), _testMethod, genericArguments, "testId", attributeParameters, true, "ExternalType");

        // Assert
        Assert.That(result.IsExternalCall, Is.True);
        Assert.That(result.ExternalTypeName, Is.EqualTo("ExternalType"));
    }

    [Test]
    public void GetNull_ReturnsNullMethodCallInfo()
    {
        // Act
        var result = _pool.GetNull();

        // Assert
        Assert.That(result.IsNull, Is.True);
    }

    [Test]
    public void Return_ForNonNullItem_AddsItemBackToPool()
    {
        // Arrange
        var genericArguments = new Type[0];
        var attributeParameters = new Dictionary<string, string>();
        var item = _pool.Rent(_mockClassMonitor.Object, typeof(string), _testMethod, genericArguments, "testId", attributeParameters);

        // Act
        _pool.Return(item);

        // Assert
        var newItem = _pool.Rent(_mockClassMonitor.Object, typeof(string), _testMethod, genericArguments, "testId2", attributeParameters);
        Assert.That(newItem, Is.Not.Null);
        Assert.That(newItem.IsNull, Is.False);
    }

    [Test]
    public void UseAndReturn_ForNonNullItem_ReturnsAsyncDisposable()
    {
        // Arrange
        var genericArguments = new Type[0];
        var attributeParameters = new Dictionary<string, string>();
        var item = _pool.Rent(_mockClassMonitor.Object, typeof(string), _testMethod, genericArguments, "testId", attributeParameters);

        // Act
        var disposable = _pool.UseAndReturn(item);

        // Assert
        Assert.That(disposable, Is.Not.Null);
        Assert.That(disposable, Is.InstanceOf<IAsyncDisposable>());
    }

    [Test]
    public void UseAndReturn_ForNullItem_ReturnsEmptyAsyncDisposable()
    {
        // Arrange
        var nullItem = _pool.GetNull();

        // Act
        var disposable = _pool.UseAndReturn(nullItem);

        // Assert
        Assert.That(disposable, Is.EqualTo(AsyncDisposable.Empty));
    }
}

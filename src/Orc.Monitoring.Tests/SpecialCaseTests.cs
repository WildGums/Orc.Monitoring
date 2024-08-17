namespace Orc.Monitoring.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using Moq;
using Orc.Monitoring;
using Orc.Monitoring.Reporters;
using Orc.Monitoring.Filters;


[TestFixture]
public class SpecialCaseTests
{
    private Mock<IMethodCallReporter> _mockReporter;
    private Mock<IMethodFilter> _mockFilter;
    private MonitoringConfiguration _config;
    private CallStack _callStack;

    [SetUp]
    public void Setup()
    {
        MonitoringController.ResetForTesting();
        _mockReporter = new Mock<IMethodCallReporter>();
        _mockFilter = new Mock<IMethodFilter>();
        _config = new MonitoringConfiguration();
        _callStack = new CallStack(_config);

        _config.AddReporter<IMethodCallReporter>();
        _config.AddFilter(_mockFilter.Object);

        MonitoringController.Configuration = _config;
        MonitoringController.Enable();
        MonitoringController.EnableReporter(typeof(IMethodCallReporter));
    }

    [Test]
    public void StaticMethod_IsCorrectlyMonitored()
    {
        // Arrange
        var staticMethod = typeof(TestClass).GetMethod(nameof(TestClass.StaticMethod));
        _mockFilter.Setup(f => f.ShouldInclude(It.IsAny<MethodInfo>())).Returns(true);

        var monitor = new StaticMethodMonitor(typeof(TestClass), _callStack, new HashSet<string> { nameof(TestClass.StaticMethod) }, _config);

        // Act
        using (var context = monitor.StartMethod(new MethodConfiguration { Reporters = new List<IMethodCallReporter> { _mockReporter.Object } }, nameof(TestClass.StaticMethod)))
        {
            TestClass.StaticMethod();
        }

        // Assert
        _mockReporter.Verify(r => r.StartReporting(It.IsAny<IObservable<MethodLifeCycleItems.ICallStackItem>>()), Times.Once);
        Assert.That(CallStackExtensions.GetTrackedStaticMethods(_callStack).Any(m => m.Name == nameof(TestClass.StaticMethod)), Is.True);
    }

    [Test]
    public void GenericMethod_IsCorrectlyMonitored()
    {
        // Arrange
        var genericMethod = typeof(TestClass).GetMethod(nameof(TestClass.GenericMethod));
        _mockFilter.Setup(f => f.ShouldInclude(It.IsAny<MethodInfo>())).Returns(true);

        var tracker = new GenericMethodTracker();
        var monitor = new ClassMonitor(typeof(TestClass), _callStack, new HashSet<string> { nameof(TestClass.GenericMethod) }, _config);

        // Act
        using (var context = monitor.StartMethod(new MethodConfiguration
               {
                   Reporters = new List<IMethodCallReporter> { _mockReporter.Object },
                   GenericArguments = new List<Type> { typeof(int) }
               }, nameof(TestClass.GenericMethod)))
        {
            TestClass.GenericMethod<int>(5);
            tracker.TrackGenericMethodInstantiation(genericMethod, new[] { typeof(int) });
        }

        // Assert
        _mockReporter.Verify(r => r.StartReporting(It.IsAny<IObservable<MethodLifeCycleItems.ICallStackItem>>()), Times.Once);
        Assert.That(tracker.GetInstantiations(genericMethod).Any(t => t[0] == typeof(int)), Is.True);
    }

    [Test]
    public void ExtensionMethod_IsCorrectlyMonitored()
    {
        // Arrange
        var extensionMethod = typeof(TestExtensions).GetMethod(nameof(TestExtensions.ExtensionMethod));
        _mockFilter.Setup(f => f.ShouldInclude(It.IsAny<MethodInfo>())).Returns(true);

        var handler = new ExtensionMethodHandler();
        var monitor = new ClassMonitor(typeof(TestExtensions), _callStack, new HashSet<string> { nameof(TestExtensions.ExtensionMethod) }, _config);

        // Act
        using (var context = monitor.StartMethod(new MethodConfiguration { Reporters = new List<IMethodCallReporter> { _mockReporter.Object } }, nameof(TestExtensions.ExtensionMethod)))
        {
            "test".ExtensionMethod();
            handler.RegisterExtensionMethod(extensionMethod);
            handler.TrackInvocation(extensionMethod);
        }

        // Assert
        _mockReporter.Verify(r => r.StartReporting(It.IsAny<IObservable<MethodLifeCycleItems.ICallStackItem>>()), Times.Once);
        Assert.That(handler.IsExtensionMethod(extensionMethod), Is.True);
        Assert.That(handler.GetInvocationCount(extensionMethod), Is.EqualTo(1));
    }

    [Test]
    public async Task AsyncMethod_IsCorrectlyMonitoredAsync()
    {
        // Arrange
        var asyncMethod = typeof(TestClass).GetMethod(nameof(TestClass.AsyncMethodAsync));
        _mockFilter.Setup(f => f.ShouldInclude(It.IsAny<MethodInfo>())).Returns(true);

        var monitor = new ClassMonitor(typeof(TestClass), _callStack, new HashSet<string> { nameof(TestClass.AsyncMethodAsync) }, _config);

        // Act
        await using (var context = monitor.StartAsyncMethod(new MethodConfiguration { Reporters = new List<IMethodCallReporter> { _mockReporter.Object } }, nameof(TestClass.AsyncMethodAsync)))
        {
            await TestClass.AsyncMethodAsync();
        }

        // Assert
        _mockReporter.Verify(r => r.StartReporting(It.IsAny<IObservable<MethodLifeCycleItems.ICallStackItem>>()), Times.Once);
        Assert.That(CallStackExtensions.GetTrackedAsyncMethods(_callStack).Any(m => m.Name == nameof(TestClass.AsyncMethodAsync)), Is.True);
    }

    [Test]
    public void OverloadedMethods_AreCorrectlyDistinguished()
    {
        // Arrange
        var method1 = typeof(TestClass).GetMethod(nameof(TestClass.OverloadedMethod), new[] { typeof(int) });
        var method2 = typeof(TestClass).GetMethod(nameof(TestClass.OverloadedMethod), new[] { typeof(string) });
        _mockFilter.Setup(f => f.ShouldInclude(It.IsAny<MethodInfo>())).Returns(true);

        var monitor = new ClassMonitor(typeof(TestClass), _callStack, new HashSet<string> { nameof(TestClass.OverloadedMethod) }, _config);

        // Act
        using (var context1 = monitor.StartMethod(new MethodConfiguration
               {
                   Reporters = new List<IMethodCallReporter> { _mockReporter.Object },
                   ParameterTypes = new List<Type> { typeof(int) }
               }, nameof(TestClass.OverloadedMethod)))
        {
            TestClass.OverloadedMethod(5);
        }

        using (var context2 = monitor.StartMethod(new MethodConfiguration
               {
                   Reporters = new List<IMethodCallReporter> { _mockReporter.Object },
                   ParameterTypes = new List<Type> { typeof(string) }
               }, nameof(TestClass.OverloadedMethod)))
        {
            TestClass.OverloadedMethod("test");
        }

        // Assert
        _mockReporter.Verify(r => r.StartReporting(It.IsAny<IObservable<MethodLifeCycleItems.ICallStackItem>>()), Times.Exactly(2));
        Assert.That(CallStackExtensions.GetTrackedMethods(_callStack).Count(m => m.Name == nameof(TestClass.OverloadedMethod)), Is.EqualTo(2));
    }
}

public static class TestClass
{
    public static void StaticMethod() { }
    public static T GenericMethod<T>(T input) => input;
    public static async Task AsyncMethodAsync() => await Task.Delay(1);
    public static void OverloadedMethod(int input) { }
    public static void OverloadedMethod(string input) { }
}

public static class TestExtensions
{
    public static void ExtensionMethod(this string input) { }
}

public static class CallStackExtensions
{
    public static IEnumerable<MethodInfo> GetTrackedStaticMethods(this CallStack callStack)
    {
        // Implement this method based on your CallStack implementation
        throw new NotImplementedException();
    }

    public static IEnumerable<MethodInfo> GetTrackedAsyncMethods(this CallStack callStack)
    {
        // Implement this method based on your CallStack implementation
        throw new NotImplementedException();
    }

    public static IEnumerable<MethodInfo> GetTrackedMethods(this CallStack callStack)
    {
        // Implement this method based on your CallStack implementation
        throw new NotImplementedException();
    }
}

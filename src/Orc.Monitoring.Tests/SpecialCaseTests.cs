﻿namespace Orc.Monitoring.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using Moq;
using Orc.Monitoring;
using Reporters;
using Filters;


[TestFixture]
public class SpecialCaseTests
{
    private MockReporter _mockReporter;
    private Mock<IMethodFilter> _mockFilter;
    private MonitoringConfiguration _config;
    private CallStack _callStack;

    [SetUp]
    public void Setup()
    {
        MonitoringController.ResetForTesting();
        _mockReporter = new MockReporter();
        _mockFilter = new Mock<IMethodFilter>();
        _config = new MonitoringConfiguration();
        _callStack = new CallStack(_config);

        _config.AddReporter(_mockReporter.GetType());
        _config.AddFilter(_mockFilter.Object);

        MonitoringController.Configuration = _config;
        MonitoringController.Enable();
        MonitoringController.EnableReporter(_mockReporter.GetType());

        _mockFilter.Setup(f => f.ShouldInclude(It.IsAny<MethodCallInfo>())).Returns(true);

        Console.WriteLine($"Initial setup - IsEnabled: {MonitoringController.IsEnabled}, MockReporter enabled: {MonitoringController.IsReporterEnabled(_mockReporter.GetType())}");
    }

    [Test]
    public void StaticMethod_IsCorrectlyMonitored()
    {
        var staticMethod = typeof(TestClass).GetMethod(nameof(TestClass.StaticMethod));
        _mockFilter.Setup(f => f.ShouldInclude(It.IsAny<MethodCallInfo>())).Returns(true);

        var monitor = new ClassMonitor(typeof(TestClass), _callStack, new HashSet<string> { nameof(TestClass.StaticMethod) }, _config);

        Console.WriteLine($"Before static method - IsEnabled: {MonitoringController.IsEnabled}, MockReporter enabled: {MonitoringController.IsReporterEnabled(_mockReporter.GetType())}");

        using (var context = monitor.StartMethod(new MethodConfiguration
               {
                   Reporters = new List<IMethodCallReporter> { _mockReporter }
               }, nameof(TestClass.StaticMethod)))
        {
            TestClass.StaticMethod();
        }

        Console.WriteLine($"After static method - StartReporting called: {_mockReporter.StartReportingCallCount}");
        Assert.That(_mockReporter.StartReportingCallCount, Is.EqualTo(1), "StartReporting should be called once for static method");
    }

    [Test]
    public void GenericMethod_IsCorrectlyMonitored()
    {
        var genericMethod = typeof(TestClass).GetMethod(nameof(TestClass.GenericMethod));
        _mockFilter.Setup(f => f.ShouldInclude(It.IsAny<MethodInfo>())).Returns(true);

        var tracker = new GenericMethodTracker();
        var monitor = new ClassMonitor(typeof(TestClass), _callStack, new HashSet<string> { nameof(TestClass.GenericMethod) }, _config);

        Console.WriteLine($"Before generic method - IsEnabled: {MonitoringController.IsEnabled}, MockReporter enabled: {MonitoringController.IsReporterEnabled(_mockReporter.GetType())}");

        using (var context = monitor.StartMethod(new MethodConfiguration
               {
                   Reporters = new List<IMethodCallReporter> { _mockReporter },
                   GenericArguments = new List<Type> { typeof(int) }
               }, nameof(TestClass.GenericMethod)))
        {
            TestClass.GenericMethod<int>(5);
            tracker.TrackGenericMethodInstantiation(genericMethod, new[] { typeof(int) });
        }

        Console.WriteLine($"After generic method - StartReporting called: {_mockReporter.StartReportingCallCount}");
        Assert.That(_mockReporter.StartReportingCallCount, Is.EqualTo(1), "StartReporting should be called once for generic method");
        Assert.That(tracker.GetInstantiations(genericMethod).Any(t => t[0] == typeof(int)), Is.True);
    }

    [Test]
    public async Task AsyncMethod_IsCorrectlyMonitoredAsync()
    {
        var asyncMethod = typeof(TestClass).GetMethod(nameof(TestClass.AsyncMethodAsync));
        _mockFilter.Setup(f => f.ShouldInclude(It.IsAny<MethodCallInfo>())).Returns(true);

        var monitor = new ClassMonitor(typeof(TestClass), _callStack, new HashSet<string> { nameof(TestClass.AsyncMethodAsync) }, _config);

        Console.WriteLine($"Before async method - IsEnabled: {MonitoringController.IsEnabled}, MockReporter enabled: {MonitoringController.IsReporterEnabled(_mockReporter.GetType())}");

        var completionSource = new TaskCompletionSource<bool>();
        _mockReporter.OnStartReporting = _ => completionSource.SetResult(true);

        await using (var context = monitor.StartAsyncMethod(new MethodConfiguration
                     {
                         Reporters = new List<IMethodCallReporter> { _mockReporter },
                         ParameterTypes = new List<Type>() // Empty list for no parameters
                     }, nameof(TestClass.AsyncMethodAsync)))
        {
            await TestClass.AsyncMethodAsync();
        }

        await Task.WhenAny(completionSource.Task, Task.Delay(5000)); // 5 second timeout

        Console.WriteLine($"After async method - StartReporting called: {_mockReporter.StartReportingCallCount}");
        Assert.That(_mockReporter.StartReportingCallCount, Is.EqualTo(1), "StartReporting should be called once for async method");
    }

    [Test]
    public void ExtensionMethod_IsCorrectlyMonitored()
    {
        var extensionMethod = typeof(TestExtensions).GetMethod(nameof(TestExtensions.ExtensionMethod));
        _mockFilter.Setup(f => f.ShouldInclude(It.IsAny<MethodInfo>())).Returns(true);

        var handler = new ExtensionMethodHandler();
        var monitor = new ClassMonitor(typeof(TestExtensions), _callStack, new HashSet<string> { nameof(TestExtensions.ExtensionMethod) }, _config);

        Console.WriteLine($"Before extension method - IsEnabled: {MonitoringController.IsEnabled}, MockReporter enabled: {MonitoringController.IsReporterEnabled(_mockReporter.GetType())}");

        using (var context = monitor.StartMethod(new MethodConfiguration { Reporters = new List<IMethodCallReporter> { _mockReporter } }, nameof(TestExtensions.ExtensionMethod)))
        {
            "test".ExtensionMethod();
            handler.RegisterExtensionMethod(extensionMethod);
            handler.TrackInvocation(extensionMethod);
        }

        Console.WriteLine($"After extension method - StartReporting called: {_mockReporter.StartReportingCallCount}");
        Assert.That(_mockReporter.StartReportingCallCount, Is.EqualTo(1), "StartReporting should be called once for extension method");
        Assert.That(handler.IsExtensionMethod(extensionMethod), Is.True);
        Assert.That(handler.GetInvocationCount(extensionMethod), Is.EqualTo(1));
    }

    [Test]
    public void OverloadedMethods_AreCorrectlyDistinguished()
    {
        var method1 = typeof(TestClass).GetMethod(nameof(TestClass.OverloadedMethod), new[] { typeof(int) });
        var method2 = typeof(TestClass).GetMethod(nameof(TestClass.OverloadedMethod), new[] { typeof(string) });
        _mockFilter.Setup(f => f.ShouldInclude(It.IsAny<MethodInfo>())).Returns(true);

        var monitor = new ClassMonitor(typeof(TestClass), _callStack, new HashSet<string> { nameof(TestClass.OverloadedMethod) }, _config);

        Console.WriteLine($"Before overloaded methods - IsEnabled: {MonitoringController.IsEnabled}, MockReporter enabled: {MonitoringController.IsReporterEnabled(_mockReporter.GetType())}");

        using (var context1 = monitor.StartMethod(new MethodConfiguration
               {
                   Reporters = new List<IMethodCallReporter> { _mockReporter },
                   ParameterTypes = new List<Type> { typeof(int) }
               }, nameof(TestClass.OverloadedMethod)))
        {
            TestClass.OverloadedMethod(5);
        }

        using (var context2 = monitor.StartMethod(new MethodConfiguration
               {
                   Reporters = new List<IMethodCallReporter> { _mockReporter },
                   ParameterTypes = new List<Type> { typeof(string) }
               }, nameof(TestClass.OverloadedMethod)))
        {
            TestClass.OverloadedMethod("test");
        }

        Console.WriteLine($"After overloaded methods - StartReporting called: {_mockReporter.StartReportingCallCount}");
        Assert.That(_mockReporter.StartReportingCallCount, Is.EqualTo(2), "StartReporting should be called twice for two different overloaded methods");
    }
}

public static class TestClass
{
    public static void StaticMethod() { }
    public static T GenericMethod<T>(T input) => input;
    public static async Task AsyncMethodAsync()
    {
        await Task.Delay(1);
    }
    public static void OverloadedMethod(int input) { }
    public static void OverloadedMethod(string input) { }
}

public static class TestExtensions
{
    public static void ExtensionMethod(this string input) { }
}

public class GenericMethodTracker
{
    private readonly Dictionary<MethodInfo, List<Type[]>> _instantiations = new Dictionary<MethodInfo, List<Type[]>>();

    public void TrackGenericMethodInstantiation(MethodInfo method, Type[] typeArguments)
    {
        if (!_instantiations.ContainsKey(method))
        {
            _instantiations[method] = new List<Type[]>();
        }
        _instantiations[method].Add(typeArguments);
    }

    public IEnumerable<Type[]> GetInstantiations(MethodInfo method)
    {
        return _instantiations.TryGetValue(method, out var instantiations) ? instantiations : Enumerable.Empty<Type[]>();
    }
}

public class ExtensionMethodHandler
{
    private readonly HashSet<MethodInfo> _extensionMethods = new HashSet<MethodInfo>();
    private readonly Dictionary<MethodInfo, int> _invocationCounts = new Dictionary<MethodInfo, int>();

    public void RegisterExtensionMethod(MethodInfo method)
    {
        _extensionMethods.Add(method);
    }

    public bool IsExtensionMethod(MethodInfo method)
    {
        return _extensionMethods.Contains(method);
    }

    public void TrackInvocation(MethodInfo method)
    {
        if (!_invocationCounts.ContainsKey(method))
        {
            _invocationCounts[method] = 0;
        }
        _invocationCounts[method]++;
    }

    public int GetInvocationCount(MethodInfo method)
    {
        return _invocationCounts.TryGetValue(method, out var count) ? count : 0;
    }
}

namespace Orc.Monitoring.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using Moq;
using Monitoring;
using Filters;
using Microsoft.Extensions.Logging;

[TestFixture]
public class SpecialCaseTests
{
    private MockReporter _mockReporter;
    private Mock<IMethodFilter> _mockFilter;
    private MonitoringConfiguration _config;
    private CallStack _callStack;
    private MethodCallInfoPool _methodCallInfoPool;
    private TestLogger<SpecialCaseTests> _logger;
    private TestLoggerFactory<SpecialCaseTests> _loggerFactory;
    private IMonitoringController _monitoringController;
    private MethodCallContextFactory _methodCallContextFactory;

    [SetUp]
    public void Setup()
    {
        _logger = new TestLogger<SpecialCaseTests>();
        _loggerFactory = new TestLoggerFactory<SpecialCaseTests>(_logger);
        _monitoringController = new MonitoringController(_loggerFactory);

        _logger.LogInformation("Setup started");

        _mockReporter = new MockReporter(_loggerFactory);
        _mockFilter = new Mock<IMethodFilter>();
        _config = new MonitoringConfiguration();
        _methodCallInfoPool = new MethodCallInfoPool(_monitoringController, _loggerFactory);
        _callStack = new CallStack(_monitoringController, _config, _methodCallInfoPool, _loggerFactory);
        _methodCallContextFactory = new MethodCallContextFactory(_monitoringController, _loggerFactory, _methodCallInfoPool);

        _config.AddReporter(_mockReporter.GetType());
        _config.AddFilter(_mockFilter.Object);

        _monitoringController.Configuration = _config;
        _monitoringController.Enable();
        _monitoringController.EnableReporter(_mockReporter.GetType());

        _mockFilter.Setup(f => f.ShouldInclude(It.IsAny<MethodCallInfo>())).Returns(true);

        _logger.LogInformation($"Initial setup - IsEnabled: {_monitoringController.IsEnabled}, MockReporter enabled: {_monitoringController.IsReporterEnabled(_mockReporter.GetType())}");
    }

    [Test]
    public void StaticMethod_IsCorrectlyMonitored()
    {
        _mockFilter.Setup(f => f.ShouldInclude(It.IsAny<MethodCallInfo>())).Returns(true);

        var monitor = new ClassMonitor(_monitoringController, typeof(TestClass), _callStack, _config, _loggerFactory, _methodCallContextFactory, _methodCallInfoPool);

        _logger.LogInformation($"Before static method - IsEnabled: {_monitoringController.IsEnabled}, MockReporter enabled: {_monitoringController.IsReporterEnabled(_mockReporter.GetType())}");

        using (var _ = monitor.StartMethod(new MethodConfiguration
        {
            Reporters = [_mockReporter]
        }, nameof(TestClass.StaticMethod)))
        {
            TestClass.StaticMethod();
        }

        _logger.LogInformation($"After static method - StartReporting called: {_mockReporter.StartReportingCallCount}");
        Assert.That(_mockReporter.StartReportingCallCount, Is.EqualTo(1), "StartReporting should be called once for static method");
    }

    [Test]
    public void GenericMethod_IsCorrectlyMonitored()
    {
        var genericMethod = typeof(TestClass).GetMethod(nameof(TestClass.GenericMethod));
        var methodCallInfo = CreateMethodCallInfo(genericMethod);
        _mockFilter.Setup(f => f.ShouldInclude(It.IsAny<MethodCallInfo>())).Returns(true);

        var tracker = new GenericMethodTracker();
        var monitor = new ClassMonitor(_monitoringController, typeof(TestClass), _callStack, _config, _loggerFactory, _methodCallContextFactory, _methodCallInfoPool);

        _logger.LogInformation($"Before generic method - IsEnabled: {_monitoringController.IsEnabled}, MockReporter enabled: {_monitoringController.IsReporterEnabled(_mockReporter.GetType())}");

        using (var _ = monitor.StartMethod(new MethodConfiguration
        {
            Reporters = [_mockReporter],
            GenericArguments = [typeof(int)]
        }, nameof(TestClass.GenericMethod)))
        {
            TestClass.GenericMethod(5);
            tracker.TrackGenericMethodInstantiation(methodCallInfo.MethodInfo, [typeof(int)]);
        }

        _logger.LogInformation($"After generic method - StartReporting called: {_mockReporter.StartReportingCallCount}");
        Assert.That(_mockReporter.StartReportingCallCount, Is.EqualTo(1), "StartReporting should be called once for generic method");
        Assert.That(tracker.GetInstantiations(methodCallInfo.MethodInfo).Any(t => t[0] == typeof(int)), Is.True);
    }

    [Test]
    public async Task AsyncMethod_IsCorrectlyMonitoredAsync()
    {
        _mockFilter.Setup(f => f.ShouldInclude(It.IsAny<MethodCallInfo>())).Returns(true);

        var monitor = new ClassMonitor(_monitoringController, typeof(TestClass), _callStack, _config, _loggerFactory, _methodCallContextFactory, _methodCallInfoPool);

        _logger.LogInformation($"Before async method - IsEnabled: {_monitoringController.IsEnabled}, MockReporter enabled: {_monitoringController.IsReporterEnabled(_mockReporter.GetType())}");

        var completionSource = new TaskCompletionSource<bool>();
        _mockReporter.OnStartReporting = _ => completionSource.SetResult(true);

        await using (var _ = monitor.StartAsyncMethod(new MethodConfiguration
        {
            Reporters = [_mockReporter],
            ParameterTypes = [] // Empty list for no parameters
        }, nameof(TestClass.AsyncMethodAsync)))
        {
            await TestClass.AsyncMethodAsync();
        }

        await Task.WhenAny(completionSource.Task, Task.Delay(5000)); // 5 second timeout

        _logger.LogInformation($"After async method - StartReporting called: {_mockReporter.StartReportingCallCount}");
        Assert.That(_mockReporter.StartReportingCallCount, Is.EqualTo(1), "StartReporting should be called once for async method");
    }

    [Test]
    public void ExtensionMethod_IsCorrectlyMonitored()
    {
        var extensionMethod = typeof(TestExtensions).GetMethod(nameof(TestExtensions.ExtensionMethod));
        var methodCallInfo = CreateMethodCallInfo(extensionMethod);
        _mockFilter.Setup(f => f.ShouldInclude(It.IsAny<MethodCallInfo>())).Returns(true);

        var handler = new ExtensionMethodHandler();
        var monitor = new ClassMonitor(_monitoringController, typeof(TestExtensions), _callStack, _config, _loggerFactory, _methodCallContextFactory, _methodCallInfoPool);

        _logger.LogInformation($"Before extension method - IsEnabled: {_monitoringController.IsEnabled}, MockReporter enabled: {_monitoringController.IsReporterEnabled(_mockReporter.GetType())}");

        using (var _ = monitor.StartMethod(new MethodConfiguration { Reporters = [_mockReporter] }, nameof(TestExtensions.ExtensionMethod)))
        {
            "test".ExtensionMethod();
            handler.RegisterExtensionMethod(methodCallInfo);
            handler.TrackInvocation(methodCallInfo);
        }

        _logger.LogInformation($"After extension method - StartReporting called: {_mockReporter.StartReportingCallCount}");
        Assert.That(_mockReporter.StartReportingCallCount, Is.EqualTo(1), "StartReporting should be called once for extension method");
        Assert.That(handler.IsExtensionMethod(methodCallInfo), Is.True);
        Assert.That(handler.GetInvocationCount(methodCallInfo), Is.EqualTo(1));
    }

    [Test]
    public void OverloadedMethods_AreCorrectlyDistinguished()
    {
        _mockFilter.Setup(f => f.ShouldInclude(It.IsAny<MethodCallInfo>())).Returns(true);

        var monitor = new ClassMonitor(_monitoringController, typeof(TestClass), _callStack, _config, _loggerFactory, _methodCallContextFactory, _methodCallInfoPool);

        _logger.LogInformation($"Before overloaded methods - IsEnabled: {_monitoringController.IsEnabled}, MockReporter enabled: {_monitoringController.IsReporterEnabled(_mockReporter.GetType())}");

        using (var _ = monitor.StartMethod(new MethodConfiguration
        {
            Reporters = [_mockReporter],
            ParameterTypes = [typeof(int)]
        }, nameof(TestClass.OverloadedMethod)))
        {
            TestClass.OverloadedMethod(5);
        }

        using (var _ = monitor.StartMethod(new MethodConfiguration
        {
            Reporters = [_mockReporter],
            ParameterTypes = [typeof(string)]
        }, nameof(TestClass.OverloadedMethod)))
        {
            TestClass.OverloadedMethod("test");
        }

        _logger.LogInformation($"After overloaded methods - StartReporting called: {_mockReporter.StartReportingCallCount}");
        Assert.That(_mockReporter.StartReportingCallCount, Is.EqualTo(2), "StartReporting should be called twice for two different overloaded methods");
    }

    private MethodCallInfo CreateMethodCallInfo(MethodInfo methodInfo)
    {
        return _methodCallInfoPool.Rent(null, methodInfo.DeclaringType, methodInfo, Array.Empty<Type>(), Guid.NewGuid().ToString(), new Dictionary<string, string>());
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
    private readonly Dictionary<MethodInfo, List<Type[]>> _instantiations = new();

    public void TrackGenericMethodInstantiation(MethodInfo method, Type[] typeArguments)
    {
        if (!_instantiations.ContainsKey(method))
        {
            _instantiations[method] = [];
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
    private readonly HashSet<string> _extensionMethods = [];
    private readonly Dictionary<string, int> _invocationCounts = new();

    public void RegisterExtensionMethod(MethodCallInfo methodCallInfo)
    {
        if (methodCallInfo.IsExtensionMethod)
        {
            _extensionMethods.Add(GetMethodKey(methodCallInfo));
        }
    }

    public bool IsExtensionMethod(MethodCallInfo methodCallInfo)
    {
        return _extensionMethods.Contains(GetMethodKey(methodCallInfo));
    }

    public void TrackInvocation(MethodCallInfo methodCallInfo)
    {
        var key = GetMethodKey(methodCallInfo);
        _invocationCounts.TryAdd(key, 0);
        _invocationCounts[key]++;
    }

    public int GetInvocationCount(MethodCallInfo methodCallInfo)
    {
        var key = GetMethodKey(methodCallInfo);
        return _invocationCounts.GetValueOrDefault(key, 0);
    }

    private string GetMethodKey(MethodCallInfo methodCallInfo)
    {
        // Create a unique key for the method based on its full name and parameter types
        var parameterTypes = methodCallInfo.MethodInfo?.GetParameters().Select(p => p.ParameterType.FullName) ?? [];
        return $"{methodCallInfo.ClassType?.Name ?? string.Empty}.{methodCallInfo.MethodName}({string.Join(",", parameterTypes)})";
    }
}

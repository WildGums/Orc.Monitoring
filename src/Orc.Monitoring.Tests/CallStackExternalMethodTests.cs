#pragma warning disable CL0002
namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using System;
using System.Reflection;
using System.Threading.Tasks;
using Moq;
using Microsoft.Extensions.Logging;


[TestFixture]
public class CallStackExternalMethodTests
{
    private CallStack _callStack;
    private Mock<IMonitoringController> _mockController;
    private Mock<IMonitoringLoggerFactory> _mockLoggerFactory;
    private Mock<ILogger<CallStack>> _mockLogger;
    private Mock<IClassMonitor> _mockClassMonitor;
    private MonitoringConfiguration _config;
    private MethodCallInfoPool _methodCallInfoPool;

    [SetUp]
    public void Setup()
    {
        _mockController = new Mock<IMonitoringController>();
        _mockLoggerFactory = new Mock<IMonitoringLoggerFactory>();
        _mockLogger = new Mock<ILogger<CallStack>>();
        _mockClassMonitor = new Mock<IClassMonitor>();
        _config = new MonitoringConfiguration();

        _mockLoggerFactory.Setup(f => f.CreateLogger<CallStack>()).Returns(_mockLogger.Object);
        _mockController.Setup(c => c.IsEnabled).Returns(true);
        _mockController.Setup(c => c.GetCurrentVersion()).Returns(new MonitoringVersion(1, 0, Guid.NewGuid()));

        _methodCallInfoPool = new MethodCallInfoPool(_mockController.Object, _mockLoggerFactory.Object);
        _callStack = new CallStack(_mockController.Object, _config, _methodCallInfoPool, _mockLoggerFactory.Object);
    }

    [Test]
    public void CreateMethodCallInfo_ForExternalMethod_SetsExternalCallProperties()
    {
        // Arrange
        var externalType = typeof(string);
        var externalMethodName = "Substring";
        var config = new MethodCallContextConfig
        {
            CallerMethodName = externalMethodName,
            ParameterTypes = new[] { typeof(int) }
        };

        // Act
        var methodCallInfo = _callStack.CreateMethodCallInfo(_mockClassMonitor.Object, externalType, config, externalType.GetMethod(externalMethodName, new[] { typeof(int) }), true, "System.String");

        // Assert
        Assert.That(methodCallInfo, Is.Not.Null);
        Assert.That(methodCallInfo.IsExternalCall, Is.True);
        Assert.That(methodCallInfo.ExternalTypeName, Is.EqualTo("System.String"));
        Assert.That(methodCallInfo.MethodName, Does.Contain("Substring"));
    }

    [Test]
    public void Push_ExternalMethodCall_SetsCorrectParent()
    {
        // Arrange
        var internalMethod = CreateMethodCallInfo("InternalMethod", typeof(CallStackExternalMethodTests));
        var externalMethod = CreateExternalMethodCallInfo("ExternalMethod", typeof(string));

        // Act
        _callStack.Push(internalMethod);
        _callStack.Push(externalMethod);

        // Assert
        Assert.That(externalMethod.Parent, Is.EqualTo(internalMethod));
        Assert.That(externalMethod.Level, Is.EqualTo(internalMethod.Level + 1));
    }

    [Test]
    public void Pop_ExternalMethodCall_MaintainsCorrectStack()
    {
        // Arrange
        var internalMethod = CreateMethodCallInfo("InternalMethod", typeof(CallStackExternalMethodTests));
        var externalMethod = CreateExternalMethodCallInfo("ExternalMethod", typeof(string));

        _callStack.Push(internalMethod);
        _callStack.Push(externalMethod);

        // Act
        _callStack.Pop(externalMethod);

        // Assert
        var nextMethod = CreateMethodCallInfo("NextMethod", typeof(CallStackExternalMethodTests));
        _callStack.Push(nextMethod);

        Assert.That(nextMethod.Parent, Is.EqualTo(internalMethod));
        Assert.That(nextMethod.Level, Is.EqualTo(internalMethod.Level + 1));
    }

    [Test]
    public async Task AsyncExternalMethodCall_MaintainsCorrectStack()
    {
        // Arrange
        var internalMethod = CreateMethodCallInfo("InternalMethod", typeof(CallStackExternalMethodTests));
        var externalMethod = CreateExternalMethodCallInfo("ExternalAsyncMethod", typeof(Task));

        _callStack.Push(internalMethod);

        // Act
        await Task.Run(() =>
        {
            _callStack.Push(externalMethod);
            // Simulate some async work
            Task.Delay(100).Wait();
            _callStack.Pop(externalMethod);
        });

        // Assert
        var nextMethod = CreateMethodCallInfo("NextMethod", typeof(CallStackExternalMethodTests));
        _callStack.Push(nextMethod);

        Assert.That(nextMethod.Parent, Is.EqualTo(internalMethod));
        Assert.That(nextMethod.Level, Is.EqualTo(internalMethod.Level + 1));
    }

    private MethodCallInfo CreateMethodCallInfo(string methodName, Type declaringType)
    {
        var config = new MethodCallContextConfig
        {
            ClassType = declaringType,
            CallerMethodName = methodName
        };

        var methodInfo = declaringType.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        if (methodInfo is null)
        {
            // If the method doesn't exist, create a dummy one for testing purposes
            methodInfo = new DummyMethodInfo(methodName, declaringType);
        }

        return _callStack.CreateMethodCallInfo(_mockClassMonitor.Object, declaringType, config, methodInfo);
    }

    private MethodCallInfo CreateExternalMethodCallInfo(string methodName, Type externalType)
    {
        var config = new MethodCallContextConfig
        {
            CallerMethodName = methodName
        };

        var methodInfo = new DummyMethodInfo(methodName, externalType);

        return _callStack.CreateMethodCallInfo(_mockClassMonitor.Object, externalType, config, methodInfo, true, externalType.FullName);
    }

    // Dummy methods to satisfy the reflection calls
    public void InternalMethod() { }
    public void NextMethod() { }

    private class DummyMethodInfo : MethodInfo
    {
        public DummyMethodInfo(string name, Type declaringType)
        {
            Name = name;
            DeclaringType = declaringType;
        }

        public override string Name { get; }
        public override Type DeclaringType { get; }

        // Implement other MethodInfo members with default implementations
        public override MethodAttributes Attributes => MethodAttributes.Public;
        public override RuntimeMethodHandle MethodHandle => throw new NotImplementedException();
        public override Type ReflectedType => DeclaringType;
        public override MethodInfo GetBaseDefinition() => this;
        public override ICustomAttributeProvider ReturnTypeCustomAttributes { get; }
        public override object[] GetCustomAttributes(bool inherit) => Array.Empty<object>();
        public override object[] GetCustomAttributes(Type attributeType, bool inherit) => Array.Empty<object>();
        public override MethodImplAttributes GetMethodImplementationFlags() => MethodImplAttributes.Managed;
        public override ParameterInfo[] GetParameters() => Array.Empty<ParameterInfo>();
        public override object Invoke(object obj, BindingFlags invokeAttr, Binder binder, object[] parameters, System.Globalization.CultureInfo culture) => null;
        public override bool IsDefined(Type attributeType, bool inherit) => false;
    }
}

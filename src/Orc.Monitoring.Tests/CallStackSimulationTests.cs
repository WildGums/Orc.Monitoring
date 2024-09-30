namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using Core.Abstractions;
using Core.CallStacks;
using Core.Configuration;
using Core.Controllers;
using Core.MethodCallContexts;
using Core.Models;
using Core.Pooling;
using Moq;
using TestUtilities.Logging;
using TestUtilities.TestHelpers;
using Utilities.Metadata;

[TestFixture]
public class CallStackSimulationTests
{
    private TestLogger<CallStackSimulationTests> _logger;
    private TestLoggerFactory<CallStackSimulationTests> _loggerFactory;
    private CallStack? _callStack;
    private Mock<IClassMonitor>? _mockClassMonitor;
    private List<MethodCallInfo> _methodCalls;
    private IMonitoringController _monitoringController;
    private MethodCallInfoPool _methodCallInfoPool;

    [SetUp]
    public void Setup()
    {
        _logger = new TestLogger<CallStackSimulationTests>();
        _loggerFactory = new TestLoggerFactory<CallStackSimulationTests>(_logger);

        _monitoringController = new MonitoringController(_loggerFactory);

        _methodCallInfoPool = new MethodCallInfoPool(_monitoringController, _loggerFactory);
        _callStack = new CallStack(_monitoringController, _methodCallInfoPool, _loggerFactory);
        _mockClassMonitor = new Mock<IClassMonitor>();
        _methodCalls = [];

        _monitoringController.Enable();
    }

    [TearDown]
    public void TearDown()
    {
        _monitoringController.Disable();
    }

    [Test]
    public void SimulateComplexCallHierarchy_VerifyRelationships()
    {
        var serviceA = new SimulatedServiceA(this);

        serviceA.MethodA1();

        VerifyCallStack();
        VerifyMethodRelationships();
    }

    public MethodCallInfo CreateMethodCallInfo(string methodName, Type declaringType)
    {
        var config = new MethodCallContextConfig
        {
            ClassType = declaringType,
            CallerMethodName = methodName
        };

        var testMethod = new TestMethodInfo(methodName, declaringType);
        testMethod.SetCustomAttribute(new MethodCallParameterAttribute("TestParam", "TestValue"));

        var methodCallInfo = _callStack!.CreateMethodCallInfo(_mockClassMonitor!.Object, declaringType, config, testMethod);
        _methodCalls.Add(methodCallInfo);
        return methodCallInfo;
    }

    private void VerifyCallStack()
    {
        var threadCallStacksField = _callStack?.GetType().GetField("_threadCallStacks", BindingFlags.NonPublic | BindingFlags.Instance);
        var threadCallStacks = threadCallStacksField?.GetValue(_callStack) as ConcurrentDictionary<int, Stack<MethodCallInfo>>;

        Assert.That(threadCallStacks, Is.Not.Null, "ThreadCallStacks should not be null");
        Assert.That(threadCallStacks!.IsEmpty, Is.True, "Call stack should be empty after all methods are popped");
    }

    private void VerifyMethodRelationships()
    {
        Assert.Multiple(() =>
        {
            // ServiceA.MethodA1
            Assert.That(_methodCalls[0].Parent, Is.EqualTo(_methodCallInfoPool.GetNull()), "MethodA1 should have no parent");
            Assert.That(_methodCalls[0].Level, Is.EqualTo(1), "MethodA1 should be at level 1");

            // ServiceB.MethodB1
            Assert.That(_methodCalls[1].Parent, Is.EqualTo(_methodCalls[0]), "MethodB1's parent should be MethodA1");
            Assert.That(_methodCalls[1].Level, Is.EqualTo(2), "MethodB1 should be at level 2");

            // ServiceC.MethodC1
            Assert.That(_methodCalls[2].Parent, Is.EqualTo(_methodCalls[1]), "MethodC1's parent should be MethodB1");
            Assert.That(_methodCalls[2].Level, Is.EqualTo(3), "MethodC1 should be at level 3");

            // ServiceB.MethodB2
            Assert.That(_methodCalls[3].Parent, Is.EqualTo(_methodCalls[1]), "MethodB2's parent should be MethodB1");
            Assert.That(_methodCalls[3].Level, Is.EqualTo(3), "MethodB2 should be at level 3");

            // ServiceC.MethodC2
            Assert.That(_methodCalls[4].Parent, Is.EqualTo(_methodCalls[3]), "MethodC2's parent should be MethodB2");
            Assert.That(_methodCalls[4].Level, Is.EqualTo(4), "MethodC2 should be at level 4");
        });
    }

    private class SimulatedServiceA(CallStackSimulationTests testFixture)
    {
        private readonly SimulatedServiceB _serviceB = new(testFixture);

        public void MethodA1()
        {
            var methodInfo = testFixture.CreateMethodCallInfo(nameof(MethodA1), typeof(SimulatedServiceA));
            testFixture._callStack!.Push(methodInfo);

            _serviceB.MethodB1();

            testFixture._callStack!.Pop(methodInfo);
        }
    }

    private class SimulatedServiceB(CallStackSimulationTests testFixture)
    {
        private readonly SimulatedServiceC _serviceC = new(testFixture);

        public void MethodB1()
        {
            var methodInfo = testFixture.CreateMethodCallInfo(nameof(MethodB1), typeof(SimulatedServiceB));
            testFixture._callStack!.Push(methodInfo);

            _serviceC.MethodC1();
            MethodB2();

            testFixture._callStack!.Pop(methodInfo);
        }

        public void MethodB2()
        {
            var methodInfo = testFixture.CreateMethodCallInfo(nameof(MethodB2), typeof(SimulatedServiceB));
            testFixture._callStack!.Push(methodInfo);

            _serviceC.MethodC2();

            testFixture._callStack!.Pop(methodInfo);
        }
    }

    private class SimulatedServiceC(CallStackSimulationTests testFixture)
    {
        public void MethodC1()
        {
            var methodInfo = testFixture.CreateMethodCallInfo(nameof(MethodC1), typeof(SimulatedServiceC));
            testFixture._callStack!.Push(methodInfo);

            // Some work here

            testFixture._callStack!.Pop(methodInfo);
        }

        public void MethodC2()
        {
            var methodInfo = testFixture.CreateMethodCallInfo(nameof(MethodC2), typeof(SimulatedServiceC));
            testFixture._callStack!.Push(methodInfo);

            // Some work here

            testFixture._callStack!.Pop(methodInfo);
        }
    }
}

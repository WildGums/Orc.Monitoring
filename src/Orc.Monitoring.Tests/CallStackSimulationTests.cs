namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using Moq;


[TestFixture]
public class CallStackSimulationTests
{
    private CallStack? _callStack;
    private Mock<IClassMonitor>? _mockClassMonitor;
    private MonitoringConfiguration? _config;
    private List<MethodCallInfo> _methodCalls;

    [SetUp]
    public void Setup()
    {
        _config = new MonitoringConfiguration();
        _callStack = new CallStack(_config);
        _mockClassMonitor = new Mock<IClassMonitor>();
        _methodCalls = new List<MethodCallInfo>();

        MonitoringController.Enable();
    }

    [TearDown]
    public void TearDown()
    {
        MonitoringController.Disable();
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
            Assert.That(_methodCalls[0].Parent, Is.EqualTo(MethodCallInfo.Null), "MethodA1 should have no parent");
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

    private class SimulatedServiceA
    {
        private readonly CallStackSimulationTests _testFixture;
        private readonly SimulatedServiceB _serviceB;

        public SimulatedServiceA(CallStackSimulationTests testFixture)
        {
            _testFixture = testFixture;
            _serviceB = new SimulatedServiceB(testFixture);
        }

        public void MethodA1()
        {
            var methodInfo = _testFixture.CreateMethodCallInfo(nameof(MethodA1), typeof(SimulatedServiceA));
            _testFixture._callStack!.Push(methodInfo);

            _serviceB.MethodB1();

            _testFixture._callStack!.Pop(methodInfo);
        }
    }

    private class SimulatedServiceB
    {
        private readonly CallStackSimulationTests _testFixture;
        private readonly SimulatedServiceC _serviceC;

        public SimulatedServiceB(CallStackSimulationTests testFixture)
        {
            _testFixture = testFixture;
            _serviceC = new SimulatedServiceC(testFixture);
        }

        public void MethodB1()
        {
            var methodInfo = _testFixture.CreateMethodCallInfo(nameof(MethodB1), typeof(SimulatedServiceB));
            _testFixture._callStack!.Push(methodInfo);

            _serviceC.MethodC1();
            MethodB2();

            _testFixture._callStack!.Pop(methodInfo);
        }

        public void MethodB2()
        {
            var methodInfo = _testFixture.CreateMethodCallInfo(nameof(MethodB2), typeof(SimulatedServiceB));
            _testFixture._callStack!.Push(methodInfo);

            _serviceC.MethodC2();

            _testFixture._callStack!.Pop(methodInfo);
        }
    }

    private class SimulatedServiceC
    {
        private readonly CallStackSimulationTests _testFixture;

        public SimulatedServiceC(CallStackSimulationTests testFixture)
        {
            _testFixture = testFixture;
        }

        public void MethodC1()
        {
            var methodInfo = _testFixture.CreateMethodCallInfo(nameof(MethodC1), typeof(SimulatedServiceC));
            _testFixture._callStack!.Push(methodInfo);

            // Some work here

            _testFixture._callStack!.Pop(methodInfo);
        }

        public void MethodC2()
        {
            var methodInfo = _testFixture.CreateMethodCallInfo(nameof(MethodC2), typeof(SimulatedServiceC));
            _testFixture._callStack!.Push(methodInfo);

            // Some work here

            _testFixture._callStack!.Pop(methodInfo);
        }
    }
}

﻿#pragma warning disable IDISP001
#pragma warning disable CL0002
namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using System;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using Moq;


[TestFixture]
public class CallStackParentChildTests
{
    private CallStack? _callStack;
    private Mock<IClassMonitor>? _mockClassMonitor;
    private MonitoringConfiguration? _config;

    [SetUp]
    public void Setup()
    {
        _config = new MonitoringConfiguration();
        _callStack = new CallStack(_config);
        _mockClassMonitor = new Mock<IClassMonitor>();

        MonitoringController.Enable();
    }

    [TearDown]
    public void TearDown()
    {
        MonitoringController.Disable();
    }

    [Test]
    public void SimpleParentChildRelationship_SetsParentCorrectly()
    {
        var parentInfo = CreateMethodCallInfo("ParentMethod");
        _callStack?.Push(parentInfo);

        var childInfo = CreateMethodCallInfo("ChildMethod");
        _callStack?.Push(childInfo);

        Assert.That(childInfo.Parent, Is.EqualTo(parentInfo));
        Assert.That(childInfo.ParentThreadId, Is.EqualTo(parentInfo.ThreadId));
        Assert.That(childInfo.Level, Is.EqualTo(parentInfo.Level + 1));
    }

    [Test]
    public void NestedMethodCalls_SetsMultipleLevelsCorrectly()
    {
        var level1 = CreateMethodCallInfo("Level1");
        var level2 = CreateMethodCallInfo("Level2");
        var level3 = CreateMethodCallInfo("Level3");

        _callStack?.Push(level1);
        _callStack?.Push(level2);
        _callStack?.Push(level3);

        Console.WriteLine($"Level1: {level1}");
        Console.WriteLine($"Level2: {level2}");
        Console.WriteLine($"Level3: {level3}");

        Assert.That(level3.Parent, Is.EqualTo(level2));
        Assert.That(level2.Parent, Is.EqualTo(level1));
        Assert.That(level1.Parent, Is.EqualTo(MethodCallInfo.Null));

        Assert.That(level3.Level, Is.EqualTo(3));
        Assert.That(level2.Level, Is.EqualTo(2));
        Assert.That(level1.Level, Is.EqualTo(1));
    }

    [Test]
    public async Task AsyncMethodCalls_MaintainsParentChildRelationship()
    {
        var parentInfo = CreateMethodCallInfo("AsyncParentMethod");
        _callStack?.Push(parentInfo);

        var childInfoTask = Task.Run(() => {
            var childInfo = CreateMethodCallInfo("AsyncChildMethod");
            _callStack?.Push(childInfo);
            return childInfo;
        });

        var childInfo = await childInfoTask;

        Assert.That(childInfo.Parent, Is.EqualTo(parentInfo));
        Assert.That(childInfo.ParentThreadId, Is.EqualTo(parentInfo.ThreadId));
        Assert.That(childInfo.Level, Is.EqualTo(parentInfo.Level + 1));
    }

    [Test]
    public void MultiThreadedCalls_SetsParentCorrectlyAcrossThreads()
    {
        var parentInfo = CreateMethodCallInfo("ParentMethod");
        _callStack?.Push(parentInfo);

        var childInfos = new ConcurrentBag<MethodCallInfo>();
        var allChildrenPushed = new ManualResetEventSlim(false);

        var tasks = Enumerable.Range(0, 5).Select(_ => Task.Run(() =>
        {
            var childInfo = CreateMethodCallInfo("ChildMethod");
            _callStack?.Push(childInfo);
            childInfos.Add(childInfo);
            if (childInfos.Count == 5)
            {
                allChildrenPushed.Set();
            }
        })).ToArray();

        Task.WaitAll(tasks);
        allChildrenPushed.Wait(TimeSpan.FromSeconds(5));

        foreach (var childInfo in childInfos)
        {
            if (childInfo.ThreadId == parentInfo.ThreadId)
            {
                // Calls on the same thread as parent should be nested
                Assert.That(childInfo.Parent?.ThreadId, Is.EqualTo(parentInfo.ThreadId));
            }
            else
            {
                // Calls on different threads should have the root parent
                Assert.That(childInfo.Parent, Is.EqualTo(parentInfo));
            }
            Assert.That(childInfo.ParentThreadId, Is.EqualTo(parentInfo.ThreadId));
            Assert.That(childInfo.Level, Is.EqualTo(childInfo.Parent?.Level + 1));
        }
    }


    [Test]
    public void RootMethod_HasNoParent()
    {
        var rootInfo = CreateMethodCallInfo("RootMethod");
        _callStack?.Push(rootInfo);

        Assert.That(rootInfo.Parent, Is.EqualTo(MethodCallInfo.Null));
        Assert.That(rootInfo.ParentThreadId, Is.EqualTo(-1));
        Assert.That(rootInfo.Level, Is.EqualTo(1));
    }

    [Test]
    public void PopMethod_RemovesFromStackButMaintainsParent()
    {
        var parentInfo = CreateMethodCallInfo("ParentMethod");
        var childInfo = CreateMethodCallInfo("ChildMethod");

        _callStack?.Push(parentInfo);
        _callStack?.Push(childInfo);

        _callStack?.Pop(childInfo);

        Assert.That(childInfo.Parent, Is.EqualTo(parentInfo));

        var newChildInfo = CreateMethodCallInfo("NewChildMethod");
        _callStack?.Push(newChildInfo);

        Assert.That(newChildInfo.Parent, Is.EqualTo(parentInfo));
        Assert.That(newChildInfo.Level, Is.EqualTo(parentInfo.Level + 1));
    }

    private MethodCallInfo CreateMethodCallInfo(string methodName)
    {
        if (_callStack is null)
        {
            throw new InvalidOperationException("CallStack not initialized");
        }

        var config = new MethodCallContextConfig
        {
            ClassType = typeof(CallStackParentChildTests),
            CallerMethodName = methodName
        };

        var testMethod = new TestMethodInfo(methodName, typeof(CallStackParentChildTests));

        // Add the MethodCallParameterAttribute
        testMethod.SetCustomAttribute(new MethodCallParameterAttribute("TestParam", "TestValue"));

        return _callStack.CreateMethodCallInfo(_mockClassMonitor?.Object, typeof(CallStackParentChildTests), config, testMethod);
    }
}

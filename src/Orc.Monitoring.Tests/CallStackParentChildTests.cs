#pragma warning disable CL0002
namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using System;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Reflection;
using Moq;


[TestFixture]
public class CallStackParentChildTests
{
    private CallStack _callStack;
    private Mock<IClassMonitor> _mockClassMonitor;
    private MonitoringConfiguration _config;

    [SetUp]
    public void Setup()
    {
        _config = new MonitoringConfiguration();
        _callStack = new CallStack(_config);
        _mockClassMonitor = new Mock<IClassMonitor>();
#if DEBUG || TEST
        _callStack.ClearGlobalParent();
#endif

        // Enable monitoring
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
        _callStack.Push(parentInfo);

        var childInfo = CreateMethodCallInfo("ChildMethod");
        _callStack.Push(childInfo);

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

        _callStack.Push(level1);
        _callStack.Push(level2);
        _callStack.Push(level3);

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
        _callStack.Push(parentInfo);

        var childInfoTask = Task.Run(() => {
            var childInfo = CreateMethodCallInfo("AsyncChildMethod");
            _callStack.Push(childInfo);
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
        _callStack.Push(parentInfo);

        Console.WriteLine($"Parent: {parentInfo}");

        var childInfos = new ConcurrentBag<MethodCallInfo>();

        Parallel.For(0, 5, _ => {
            var childInfo = CreateMethodCallInfo("ChildMethod");
            _callStack.Push(childInfo);
            childInfos.Add(childInfo);
            Console.WriteLine($"Child pushed: {childInfo}");
        });

        Console.WriteLine("All children:");
        foreach (var childInfo in childInfos)
        {
            Console.WriteLine($"Child: {childInfo}");
            Assert.That(childInfo.Parent, Is.EqualTo(parentInfo), $"Child {childInfo.Id} should have parent {parentInfo.Id}");
            Assert.That(childInfo.ParentThreadId, Is.EqualTo(parentInfo.ThreadId), $"Child {childInfo.Id} should have parent thread ID {parentInfo.ThreadId}");
            Assert.That(childInfo.Level, Is.EqualTo(parentInfo.Level + 1), $"Child {childInfo.Id} should have level {parentInfo.Level + 1}");
        }
    }

    [Test]
    public void RootMethod_HasNoParent()
    {
        var rootInfo = CreateMethodCallInfo("RootMethod");
        _callStack.Push(rootInfo);

        Assert.That(rootInfo.Parent, Is.EqualTo(MethodCallInfo.Null));
        Assert.That(rootInfo.ParentThreadId, Is.EqualTo(-1));
        Assert.That(rootInfo.Level, Is.EqualTo(1));
    }

    [Test]
    public void PopMethod_RemovesFromStackButMaintainsParent()
    {
        var parentInfo = CreateMethodCallInfo("ParentMethod");
        var childInfo = CreateMethodCallInfo("ChildMethod");

        _callStack.Push(parentInfo);
        _callStack.Push(childInfo);

        _callStack.Pop(childInfo);

        Assert.That(childInfo.Parent, Is.EqualTo(parentInfo));

        var newChildInfo = CreateMethodCallInfo("NewChildMethod");
        _callStack.Push(newChildInfo);

        Assert.That(newChildInfo.Parent, Is.EqualTo(parentInfo));
        Assert.That(newChildInfo.Level, Is.EqualTo(parentInfo.Level + 1));
    }

    private MethodCallInfo CreateMethodCallInfo(string methodName)
    {
        var config = new MethodCallContextConfig
        {
            ClassType = typeof(CallStackParentChildTests),
            CallerMethodName = methodName
        };

        var testMethod = new TestMethodInfo(methodName, typeof(CallStackParentChildTests));

        // Add the MethodCallParameterAttribute
        testMethod.SetCustomAttribute(new MethodCallParameterAttribute("TestParam", "TestValue"));

        return _callStack.CreateMethodCallInfo(_mockClassMonitor.Object, typeof(CallStackParentChildTests), config, testMethod);
    }
}

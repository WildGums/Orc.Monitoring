#pragma warning disable IDISP001
#pragma warning disable CL0002
namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using System;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Moq;
using Reporters;

[TestFixture]
public class CallStackParentChildTests
{
    private TestLogger<CallStackParentChildTests> _logger;
    private TestLoggerFactory<CallStackParentChildTests> _loggerFactory;
    private CallStack? _callStack;
    private Mock<IClassMonitor>? _mockClassMonitor;
    private MonitoringConfiguration? _config;
    private IMonitoringController _monitoringController;
    private MethodCallInfoPool _methodCallInfoPool;

    [SetUp]
    public void Setup()
    {
        _logger = new TestLogger<CallStackParentChildTests>();
        _loggerFactory = new TestLoggerFactory<CallStackParentChildTests>(_logger);
        _config = new MonitoringConfiguration();
        _monitoringController = new MonitoringController(_loggerFactory, () => new EnhancedDataPostProcessor(_loggerFactory));
        _methodCallInfoPool = new MethodCallInfoPool(_monitoringController, _loggerFactory);
        
        _callStack = new CallStack(_monitoringController, _config, _methodCallInfoPool, _loggerFactory);
        _mockClassMonitor = new Mock<IClassMonitor>();

        _monitoringController.Enable();
    }

    [TearDown]
    public void TearDown()
    {
        _monitoringController.Disable();
    }

    [Test]
    public void SimpleParentChildRelationship_SetsParentCorrectly()
    {
        var parentInfo = CreateMethodCallInfo("ParentMethod");
        _callStack?.Push(parentInfo);

        var childInfo = CreateMethodCallInfo("ChildMethod");
        _callStack?.Push(childInfo);

        Assert.That(childInfo.Parent, Is.EqualTo(parentInfo), "Child's parent should be the parent method");
        Assert.That(childInfo.ParentThreadId, Is.EqualTo(parentInfo.ThreadId), "Child's parent thread ID should match the parent's thread ID");
        Assert.That(childInfo.Level, Is.EqualTo(parentInfo.Level + 1), "Child's level should be one more than the parent's level");
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

        Assert.That(level3.Parent, Is.EqualTo(level2), "Level3's parent should be Level2");
        Assert.That(level2.Parent, Is.EqualTo(level1), "Level2's parent should be Level1");
        Assert.That(level1.Parent, Is.EqualTo(_methodCallInfoPool.GetNull()), "Level1's parent should be null");

        Assert.That(level3.Level, Is.EqualTo(3), "Level3 should be at level 3");
        Assert.That(level2.Level, Is.EqualTo(2), "Level2 should be at level 2");
        Assert.That(level1.Level, Is.EqualTo(1), "Level1 should be at level 1");
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

        Assert.That(childInfo.Parent, Is.EqualTo(parentInfo), "Child's parent should be the parent method");
        Assert.That(childInfo.ParentThreadId, Is.EqualTo(parentInfo.ThreadId), "Child's parent thread ID should match the parent's thread ID");
        Assert.That(childInfo.Level, Is.EqualTo(parentInfo.Level + 1), "Child's level should be one more than the parent's level");
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
                Assert.That(childInfo.Parent?.ThreadId, Is.EqualTo(parentInfo.ThreadId), "Same-thread child should have the parent as its parent");
            }
            else
            {
                // Calls on different threads should have the root parent
                Assert.That(childInfo.Parent, Is.EqualTo(parentInfo), "Cross-thread child should have the parent as its parent");
            }
            Assert.That(childInfo.ParentThreadId, Is.EqualTo(parentInfo.ThreadId), "ParentThreadId should match the parent's thread ID");
            Assert.That(childInfo.Level, Is.EqualTo(childInfo.Parent?.Level + 1), "Child's level should be one more than the parent's level");
        }
    }


    [Test]
    public void RootMethod_HasNoParent()
    {
        var rootInfo = CreateMethodCallInfo("RootMethod");
        _callStack?.Push(rootInfo);

        Assert.That(rootInfo.Parent, Is.EqualTo(_methodCallInfoPool.GetNull()), "Root method should have no parent");
        Assert.That(rootInfo.ParentThreadId, Is.EqualTo(-1), "Root method should have no parent thread ID");
        Assert.That(rootInfo.Level, Is.EqualTo(1), "Root method should be at level 1");
    }

    [Test]
    public void PopMethod_RemovesFromStackButMaintainsParent()
    {
        var parentInfo = CreateMethodCallInfo("ParentMethod");
        var childInfo = CreateMethodCallInfo("ChildMethod");

        _callStack?.Push(parentInfo);
        _callStack?.Push(childInfo);

        _callStack?.Pop(childInfo);

        Assert.That(childInfo.Parent, Is.EqualTo(parentInfo), "Child should maintain parent relationship after being popped");

        var newChildInfo = CreateMethodCallInfo("NewChildMethod");
        _callStack?.Push(newChildInfo);

        Assert.That(newChildInfo.Parent, Is.EqualTo(parentInfo), "New child should have the parent as its parent");
        Assert.That(newChildInfo.Level, Is.EqualTo(parentInfo.Level + 1), "New child should be at level 2");
    }

    [Test]
    public void DeepNestedCalls_CorrectlyTracksAllLevels()
    {
        const int depth = 100; // Test with a deep call stack
        var methodInfos = new MethodCallInfo[depth];

        for (int i = 0; i < depth; i++)
        {
            methodInfos[i] = CreateMethodCallInfo($"Method{i}");
            _callStack?.Push(methodInfos[i]);
        }

        for (int i = 0; i < depth; i++)
        {
            Assert.That(methodInfos[i].Level, Is.EqualTo(i + 1), $"Level mismatch at depth {i}");
            if (i > 0)
            {
                Assert.That(methodInfos[i].Parent, Is.EqualTo(methodInfos[i - 1]), $"Parent mismatch at depth {i}");
            }
            else
            {
                Assert.That(methodInfos[i].Parent, Is.EqualTo(_methodCallInfoPool.GetNull()), "Root method should have Null parent");
            }
        }

        // Pop all methods and verify
        for (int i = depth - 1; i >= 0; i--)
        {
            _callStack?.Pop(methodInfos[i]);
        }

        var threadCallStacksField = _callStack?.GetType().GetField("_threadCallStacks", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var threadCallStacks = threadCallStacksField?.GetValue(_callStack) as ConcurrentDictionary<int, Stack<MethodCallInfo>>;

        Assert.That(threadCallStacks, Is.Not.Null, "ThreadCallStacks should not be null");
        Assert.That(threadCallStacks!.IsEmpty, Is.True, $"Call stack should be empty after popping all methods. Current state: {string.Join(", ", threadCallStacks.Select(kvp => $"{kvp.Key}: {kvp.Value.Count}"))}");
    }

    [Test]
    public void PopWithoutPush_HandlesGracefully()
    {
        var methodInfo = CreateMethodCallInfo("UnpushedMethod");

        Assert.DoesNotThrow(() => _callStack?.Pop(methodInfo), "Popping an unpushed method should not throw an exception");
    }

    [Test]
    public async Task LongRunningAsyncOperation_MaintainsCorrectRelationship()
    {
        var parentInfo = CreateMethodCallInfo("LongRunningParent");
        _callStack?.Push(parentInfo);

        var longRunningTask = Task.Run(async () =>
        {
            var childInfo = CreateMethodCallInfo("LongRunningChild");
            _callStack?.Push(childInfo);
            await Task.Delay(5000); // Simulate a long-running operation
            return childInfo;
        });

        await Task.Delay(1000); // Give some time for the long-running task to start

        var quickChildInfo = CreateMethodCallInfo("QuickChild");
        _callStack?.Push(quickChildInfo);
        _callStack?.Pop(quickChildInfo);

        var longRunningChildInfo = await longRunningTask;

        Assert.Multiple(() =>
        {
            Assert.That(quickChildInfo.Parent, Is.EqualTo(parentInfo), "Quick child should have the parent as its parent");
            Assert.That(longRunningChildInfo.Parent, Is.EqualTo(parentInfo), "Long-running child should maintain parent relationship");
            Assert.That(parentInfo.Level, Is.EqualTo(1), "Parent level should be 1");
            Assert.That(quickChildInfo.Level, Is.EqualTo(2), "Quick child level should be 2");
            Assert.That(longRunningChildInfo.Level, Is.EqualTo(2), "Long-running child level should be 2");
        });

        _callStack?.Pop(longRunningChildInfo);
        _callStack?.Pop(parentInfo);
    }

    [Test]
    public void ConcurrentPushPop_MaintainsCorrectState()
    {
        const int operationCount = 1000;
        var barrier = new Barrier(2);
        var random = new Random();

        var pushTask = Task.Run(() =>
        {
            barrier.SignalAndWait();
            for (int i = 0; i < operationCount; i++)
            {
                var methodInfo = CreateMethodCallInfo($"PushMethod{i}");
                _callStack?.Push(methodInfo);
                if (random.Next(2) == 0) // Randomly sleep to increase chance of interleaving
                    Thread.Sleep(1);
            }
        });

        var popTask = Task.Run(() =>
        {
            barrier.SignalAndWait();
            for (int i = 0; i < operationCount; i++)
            {
                var methodInfo = CreateMethodCallInfo($"PopMethod{i}");
                _callStack?.Pop(methodInfo);
                if (random.Next(2) == 0) // Randomly sleep to increase chance of interleaving
                    Thread.Sleep(1);
            }
        });

        Task.WaitAll(pushTask, popTask);

        // Verify the final state
        var finalStackField = _callStack?.GetType().GetField("_threadCallStacks", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var finalStack = (ConcurrentDictionary<int, Stack<MethodCallInfo>>?)finalStackField?.GetValue(_callStack);

        Assert.That(finalStack, Is.Not.Null, "Final stack should not be null");
        Assert.That(finalStack!.Values.Sum(stack => stack.Count), Is.LessThanOrEqualTo(operationCount), "Final stack count should not exceed operation count");
    }

    [Test]
    public void Push_WithNullMethodCallInfo_ThrowsException()
    {
        Assert.Throws<ArgumentNullException>(() => _callStack?.Push(null!));
    }

    [Test]
    public void ComplexCallSequence_MaintainsCorrectRelationships()
    {
        var method1 = CreateMethodCallInfo("MethodCall1");
        var method2 = CreateMethodCallInfo("MethodCall2");
        var method3 = CreateMethodCallInfo("MethodCall3");
        var method4 = CreateMethodCallInfo("MethodCall4");
        var method5 = CreateMethodCallInfo("MethodCall5");
        var method6 = CreateMethodCallInfo("MethodCall6");

        _callStack?.Push(method1);
        _callStack?.Push(method2);
        _callStack?.Push(method3);
        _callStack?.Push(method4);
        _callStack?.Pop(method4);
        _callStack?.Push(method5);
        _callStack?.Pop(method5);
        _callStack?.Pop(method3);
        _callStack?.Push(method6);
        _callStack?.Pop(method6);
        _callStack?.Pop(method2);
        _callStack?.Pop(method1);

        Assert.Multiple(() =>
        {
            Assert.That(method1.Parent, Is.EqualTo(_methodCallInfoPool.GetNull()), "MethodCall1 should have no parent");
            Assert.That(method1.Level, Is.EqualTo(1), "MethodCall1 should be at level 1");

            Assert.That(method2.Parent, Is.EqualTo(method1), "MethodCall2's parent should be MethodCall1");
            Assert.That(method2.Level, Is.EqualTo(2), "MethodCall2 should be at level 2");

            Assert.That(method3.Parent, Is.EqualTo(method2), "MethodCall3's parent should be MethodCall2");
            Assert.That(method3.Level, Is.EqualTo(3), "MethodCall3 should be at level 3");

            Assert.That(method4.Parent, Is.EqualTo(method3), "MethodCall4's parent should be MethodCall3");
            Assert.That(method4.Level, Is.EqualTo(4), "MethodCall4 should be at level 4");

            Assert.That(method5.Parent, Is.EqualTo(method3), "MethodCall5's parent should be MethodCall3");
            Assert.That(method5.Level, Is.EqualTo(4), "MethodCall5 should be at level 4");

            Assert.That(method6.Parent, Is.EqualTo(method2), "MethodCall6's parent should be MethodCall2");
            Assert.That(method6.Level, Is.EqualTo(3), "MethodCall6 should be at level 3");
        });

        // Verify that the call stack is empty at the end
        var threadCallStacksField = _callStack?.GetType().GetField("_threadCallStacks", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var threadCallStacks = threadCallStacksField?.GetValue(_callStack) as ConcurrentDictionary<int, Stack<MethodCallInfo>>;
        Assert.That(threadCallStacks, Is.Not.Null, "ThreadCallStacks should not be null");
        Assert.That(threadCallStacks!.IsEmpty, Is.True, "Call stack should be empty after all methods are popped");
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

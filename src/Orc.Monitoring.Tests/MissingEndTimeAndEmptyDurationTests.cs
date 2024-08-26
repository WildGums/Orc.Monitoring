#pragma warning disable CL0002
namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using System;
using System.Threading.Tasks;
using System.Threading;
using Moq;
using Orc.Monitoring.Reporters.ReportOutputs;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Orc.Monitoring.MethodLifeCycleItems;

[TestFixture]
public class MissingEndTimeAndEmptyDurationTests
{
    private CallStack _callStack;
    private Mock<IClassMonitor> _mockClassMonitor;
    private MonitoringConfiguration _config;
    private ReportOutputHelper _reportOutputHelper;
    private List<ICallStackItem> _callStackItems;
    private IDisposable _callStackObserver;

    [SetUp]
    public void Setup()
    {
        _config = new MonitoringConfiguration();
        _callStack = new CallStack(_config);
        _callStackItems = new List<ICallStackItem>();

        _callStackObserver = StartObservingCallStack();

        _mockClassMonitor = new Mock<IClassMonitor>();
        _reportOutputHelper = new ReportOutputHelper();
        MonitoringController.Enable();
    }

    [TearDown]
    public void TearDown()
    {
        _callStackObserver.Dispose();
        MonitoringController.Disable();
    }

    private IDisposable StartObservingCallStack()
    {
        return _callStack.Subscribe(item =>
        {
            Console.WriteLine($"Received call stack item: {item.GetType().Name}");
            _callStackItems.Add(item);
        });
    }

    [Test]
    public async Task TestIncompleteMethodExecution()
    {
        var methodInfo = CreateMethodCallInfo("IncompleteMethod");
        _callStack.Push(methodInfo);

        // Simulate abrupt termination without popping the method
        await Task.Delay(10);

        var reportItems = ProcessCallStackItems();
        Assert.That(reportItems, Has.Some.Matches<ReportItem>(item =>
            item.MethodName.StartsWith("IncompleteMethod") && string.IsNullOrEmpty(item.EndTime)));
    }

    [Test]
    public async Task TestAsyncMethodWithDelayedCompletion()
    {
        var methodInfo = CreateMethodCallInfo("AsyncMethod");

        _callStack.Push(methodInfo);

        Console.WriteLine("Call stack after Push:");
        LogCallStackItems();

        // Process items before the async method completes
        var reportItems = ProcessCallStackItems();

        Console.WriteLine("Report items after first processing:");
        LogReportItems(reportItems);

        // Assert that the AsyncMethod is in the report items and has no EndTime
        Assert.That(reportItems, Has.Exactly(1).Matches<ReportItem>(item =>
                item.MethodName.StartsWith("AsyncMethod") && string.IsNullOrEmpty(item.EndTime)),
            $"AsyncMethod should be present exactly once with no EndTime. Actual items: {string.Join(", ", reportItems.Select(i => $"{i.MethodName}:{i.EndTime}"))}");

        // Now complete the async method
        await Task.Delay(10); // Short delay to simulate some work
        _callStack.Pop(methodInfo);

        Console.WriteLine("Call stack after Pop:");
        LogCallStackItems();

        // Process the end of the async method
        _reportOutputHelper.ProcessCallStackItem(new MethodCallEnd(methodInfo));
        reportItems = _reportOutputHelper.GetReportItems();

        Console.WriteLine("Report items after second processing:");
        LogReportItems(reportItems);

        // Assert that the AsyncMethod now has an EndTime
        Assert.That(reportItems, Has.Exactly(1).Matches<ReportItem>(item =>
                item.MethodName.StartsWith("AsyncMethod") && !string.IsNullOrEmpty(item.EndTime)),
            $"AsyncMethod should be present exactly once with an EndTime. Actual items: {string.Join(", ", reportItems.Select(i => $"{i.MethodName}:{i.EndTime}"))}");
    }

    private void LogCallStackItems()
    {
        foreach (var item in _callStackItems)
        {
            Console.WriteLine($"  {item.GetType().Name}: {(item as MethodCallStart)?.MethodCallInfo.MethodName ?? "Unknown"}");
        }
    }

    private void LogReportItems(List<ReportItem> items)
    {
        foreach (var item in items)
        {
            Console.WriteLine($"  {item.MethodName}: StartTime={item.StartTime}, EndTime={item.EndTime}");
        }
    }

    [Test]
    public void TestExceptionDuringMethodExecution()
    {
        var methodInfo = CreateMethodCallInfo("ExceptionMethod");
        _callStack.Push(methodInfo);

        try
        {
            throw new Exception("Test exception");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Caught exception: {ex.Message}");
            // Exception caught, but method not popped from call stack
        }

        var reportItems = ProcessCallStackItems();
        var exceptionMethodReport = reportItems.FirstOrDefault(item => item.MethodName?.StartsWith("ExceptionMethod") ?? false);

        Assert.That(exceptionMethodReport, Is.Not.Null, "ExceptionMethod should be present in the report items.");
        Assert.That(exceptionMethodReport.EndTime, Is.Null, "ExceptionMethod should not have an EndTime.");
    }


    [Test]
    public void TestVeryShortMethodExecution()
    {
        var methodInfo = CreateMethodCallInfo("VeryShortMethod");
        _callStack.Push(methodInfo);
        _callStack.Pop(methodInfo);

        var reportItems = ProcessCallStackItems();
        var veryShortMethodReport = reportItems.FirstOrDefault(item => item.MethodName.StartsWith("VeryShortMethod"));

        Assert.That(veryShortMethodReport, Is.Not.Null, "VeryShortMethod should be present in the report items.");
        Assert.That(veryShortMethodReport.EndTime, Is.Not.Null, "VeryShortMethod should have an EndTime.");
        Assert.That(veryShortMethodReport.StartTime, Is.Not.Null, "VeryShortMethod should have a StartTime.");
        Assert.That(veryShortMethodReport.Duration, Is.Not.Null, "VeryShortMethod should have a Duration.");

        var duration = double.Parse(veryShortMethodReport.Duration);
        Assert.That(duration, Is.GreaterThan(0), "VeryShortMethod should have a Duration greater than 0.");
    }


    [Test]
    public async Task TestConcurrentMethodExecution()
    {
        var methodInfo1 = CreateMethodCallInfo("ConcurrentMethod1");
        var methodInfo2 = CreateMethodCallInfo("ConcurrentMethod2");

        Console.WriteLine("Starting concurrent method execution");

        var task1 = Task.Run(() =>
        {
            Console.WriteLine($"Pushing ConcurrentMethod1 (Thread: {Environment.CurrentManagedThreadId})");
            _callStack.Push(methodInfo1);
            Thread.Sleep(10);
            Console.WriteLine($"Popping ConcurrentMethod1 (Thread: {Environment.CurrentManagedThreadId})");
            _callStack.Pop(methodInfo1);
        });

        var task2 = Task.Run(() =>
        {
            Console.WriteLine($"Pushing ConcurrentMethod2 (Thread: {Environment.CurrentManagedThreadId})");
            _callStack.Push(methodInfo2);
            Thread.Sleep(5);
            Console.WriteLine($"Popping ConcurrentMethod2 (Thread: {Environment.CurrentManagedThreadId})");
            _callStack.Pop(methodInfo2);
        });

        await Task.WhenAll(task1, task2);

        Console.WriteLine("Concurrent method execution completed");

        var reportItems = ProcessCallStackItems();

        Console.WriteLine($"Report items count: {reportItems.Count}");
        foreach (var item in reportItems)
        {
            Console.WriteLine($"Method: {item.MethodName}, StartTime: {item.StartTime}, EndTime: {item.EndTime}");
        }

        Assert.That(reportItems, Has.Some.Matches<ReportItem>(item =>
            item.MethodName.StartsWith("ConcurrentMethod1") && !string.IsNullOrEmpty(item.EndTime)));
        Assert.That(reportItems, Has.Some.Matches<ReportItem>(item =>
            item.MethodName.StartsWith("ConcurrentMethod2") && !string.IsNullOrEmpty(item.EndTime)));
    }

    private MethodCallInfo CreateMethodCallInfo(string methodName)
    {
        var config = new MethodCallContextConfig
        {
            ClassType = typeof(MissingEndTimeAndEmptyDurationTests),
            CallerMethodName = methodName
        };

        var testMethodInfo = new TestMethodInfo(methodName, typeof(MissingEndTimeAndEmptyDurationTests));

        return _callStack.CreateMethodCallInfo(_mockClassMonitor.Object, typeof(MissingEndTimeAndEmptyDurationTests), config, testMethodInfo);
    }

    private List<ReportItem> ProcessCallStackItems()
    {
        foreach (var item in _callStackItems)
        {
            Console.WriteLine($"  Processing: {item.GetType().Name} for {(item as IMethodLifeCycleItem)?.MethodCallInfo.MethodName}");
            _reportOutputHelper.ProcessCallStackItem(item);
        }

        var reportItems = _reportOutputHelper.GetReportItems();
        Console.WriteLine($"Report items after processing: {reportItems.Count}");
        foreach (var item in reportItems)
        {
            Console.WriteLine($"  {item.MethodName}: StartTime={item.StartTime}, EndTime={item.EndTime}");
        }

        return reportItems;
    }
}

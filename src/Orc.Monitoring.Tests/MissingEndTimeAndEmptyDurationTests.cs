#pragma warning disable CL0002
namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using System;
using System.Threading.Tasks;
using System.Threading;
using Moq;
using Reporters.ReportOutputs;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Extensions.Logging;
using MethodLifeCycleItems;
using System.Collections.Concurrent;
using TestUtilities.Logging;
using TestUtilities.TestHelpers;

[TestFixture]
public class MissingEndTimeAndEmptyDurationTests
{
    private TestLogger<MissingEndTimeAndEmptyDurationTests> _logger;
    private TestLoggerFactory<MissingEndTimeAndEmptyDurationTests> _loggerFactory;
    private CallStack _callStack;
    private Mock<IClassMonitor> _mockClassMonitor;
    private MonitoringConfiguration _config;
    private ReportOutputHelper _reportOutputHelper;
    private List<ICallStackItem> _callStackItems;
    private IDisposable _callStackObserver;
    private IMonitoringController _monitoringController;
    private MethodCallInfoPool _methodCallInfoPool;

    [SetUp]
    public void Setup()
    {
        _logger = new TestLogger<MissingEndTimeAndEmptyDurationTests>();
        _loggerFactory = new TestLoggerFactory<MissingEndTimeAndEmptyDurationTests>(_logger);
        _loggerFactory.EnableLoggingFor<CallStack>();
        _loggerFactory.EnableLoggingFor<MonitoringController>();
        _loggerFactory.EnableLoggingFor<MethodCallInfoPool>();
        _config = new MonitoringConfiguration();
        _monitoringController = new MonitoringController(_loggerFactory);
        _methodCallInfoPool = new MethodCallInfoPool(_monitoringController, _loggerFactory);
        _callStack = new CallStack(_monitoringController, _config, _methodCallInfoPool, _loggerFactory);
        _callStackItems = [];

        _callStackObserver = StartObservingCallStack();

        _mockClassMonitor = new Mock<IClassMonitor>();
        _reportOutputHelper = new ReportOutputHelper(_loggerFactory, new ReportItemFactory(_loggerFactory));
        _monitoringController.Enable();
    }

    [TearDown]
    public void TearDown()
    {
        _callStackObserver.Dispose();
        _monitoringController.Disable();
    }

    private IDisposable StartObservingCallStack()
    {
        return _callStack.Subscribe(item =>
        {
            _logger.LogInformation($"Received call stack item: {item.GetType().Name}");
            _callStackItems.Add(item);
        });
    }

    [Test]
    public async Task TestIncompleteMethodExecution()
    {
        var methodInfo = CreateMethodCallInfo("IncompleteMethod");
        _callStack.Push(methodInfo);
        _reportOutputHelper.ProcessCallStackItem(new MethodCallStart(methodInfo));

        // Simulate abrupt termination without popping the method
        await Task.Delay(10);

        var reportItems = ProcessCallStackItems();
        Assert.That(reportItems, Has.Some.Matches<ReportItem>(item =>
            (item.MethodName ?? string.Empty).StartsWith("IncompleteMethod") && string.IsNullOrEmpty(item.EndTime)));
    }

    [Test]
    public async Task TestAsyncMethodWithDelayedCompletion()
    {
        var methodInfo = CreateMethodCallInfo("AsyncMethod");

        _callStack.Push(methodInfo);
        _reportOutputHelper.ProcessCallStackItem(new MethodCallStart(methodInfo));

        _logger.LogInformation("Call stack after Push:");
        LogCallStackItems();

        // Process items before the async method completes
        var reportItems = ProcessCallStackItems();

        _logger.LogInformation("Report items after first processing:");
        LogReportItems(reportItems);

        // Assert that the AsyncMethod is in the report items and has no EndTime
        Assert.That(reportItems, Has.Exactly(1).Matches<ReportItem>(item =>
                (item.MethodName ?? string.Empty).StartsWith("AsyncMethod") && string.IsNullOrEmpty(item.EndTime)),
            $"AsyncMethod should be present exactly once with no EndTime. Actual items: {string.Join(", ", reportItems.Select(i => $"{i.MethodName}:{i.EndTime}"))}");

        // Now complete the async method
        await Task.Delay(10); // Short delay to simulate some work
        _callStack.Pop(methodInfo);

        _logger.LogInformation("Call stack after Pop:");
        LogCallStackItems();

        // Process the end of the async method
        _reportOutputHelper.ProcessCallStackItem(new MethodCallEnd(methodInfo));
        reportItems = _reportOutputHelper.ReportItems.ToList();

        _logger.LogInformation("Report items after second processing:");
        LogReportItems(reportItems);

        // Assert that the AsyncMethod now has an EndTime
        Assert.That(reportItems, Has.Exactly(1).Matches<ReportItem>(item =>
                (item.MethodName ?? string.Empty).StartsWith("AsyncMethod") && !string.IsNullOrEmpty(item.EndTime)),
            $"AsyncMethod should be present exactly once with an EndTime. Actual items: {string.Join(", ", reportItems.Select(i => $"{i.MethodName}:{i.EndTime}"))}");
    }

    private void LogCallStackItems()
    {
        foreach (var item in _callStackItems)
        {
            _logger.LogInformation($"  {item.GetType().Name}: {(item as MethodCallStart)?.MethodCallInfo.MethodName ?? "Unknown"}");
        }
    }

    private void LogReportItems(List<ReportItem> items)
    {
        foreach (var item in items)
        {
            _logger.LogInformation($"  {item.MethodName}: StartTime={item.StartTime}, EndTime={item.EndTime}");
        }
    }

    [Test]
    public void TestExceptionDuringMethodExecution()
    {
        var methodInfo = CreateMethodCallInfo("ExceptionMethod");
        _callStack.Push(methodInfo);
        _reportOutputHelper.ProcessCallStackItem(new MethodCallStart(methodInfo));

        try
        {
            throw new Exception("Test exception");
        }
        catch (Exception ex)
        {
            _logger.LogInformation($"Caught exception: {ex.Message}");
            _reportOutputHelper.ProcessCallStackItem(new MethodCallException(methodInfo, ex));
        }
        finally
        {
            _callStack.Pop(methodInfo);
            _reportOutputHelper.ProcessCallStackItem(new MethodCallEnd(methodInfo));
        }

        var reportItems = _reportOutputHelper.ReportItems.ToList();
        var exceptionMethodReport = reportItems.FirstOrDefault(item => item.MethodName?.StartsWith("ExceptionMethod") ?? false);

        Assert.That(exceptionMethodReport, Is.Not.Null, "ExceptionMethod should be present in the report items.");
        Assert.That(exceptionMethodReport.EndTime, Is.Not.Null, "ExceptionMethod should have an EndTime.");
    }

    [Test]
    public void TestVeryShortMethodExecution()
    {
        var methodInfo = CreateMethodCallInfo("VeryShortMethod");
        _callStack.Push(methodInfo);
        _reportOutputHelper.ProcessCallStackItem(new MethodCallStart(methodInfo));
        _callStack.Pop(methodInfo);
        _reportOutputHelper.ProcessCallStackItem(new MethodCallEnd(methodInfo));

        var reportItems = ProcessCallStackItems();
        var veryShortMethodReport = reportItems.FirstOrDefault(item => (item.MethodName ?? string.Empty).StartsWith("VeryShortMethod"));

        Assert.That(veryShortMethodReport, Is.Not.Null, "VeryShortMethod should be present in the report items.");
        Assert.That(veryShortMethodReport.EndTime, Is.Not.Null, "VeryShortMethod should have an EndTime.");
        Assert.That(veryShortMethodReport.StartTime, Is.Not.Null, "VeryShortMethod should have a StartTime.");
        Assert.That(veryShortMethodReport.Duration, Is.Not.Null, "VeryShortMethod should have a Duration.");

        var duration = double.Parse(veryShortMethodReport.Duration, NumberStyles.Any, CultureInfo.InvariantCulture);
        Assert.That(duration, Is.GreaterThanOrEqualTo(0), "VeryShortMethod should have a Duration greater than 0.");
    }


    [Test]
    public async Task TestConcurrentMethodExecution()
    {
        var methodInfo1 = CreateMethodCallInfo("ConcurrentMethod1");
        var methodInfo2 = CreateMethodCallInfo("ConcurrentMethod2");

        _logger.LogInformation("Starting concurrent method execution");

        var task1 = Task.Run(() =>
        {
            _logger.LogInformation($"Pushing ConcurrentMethod1 (Thread: {Environment.CurrentManagedThreadId})");
            _callStack.Push(methodInfo1);
            _reportOutputHelper.ProcessCallStackItem(new MethodCallStart(methodInfo1));
            Thread.Sleep(10);
            _logger.LogInformation($"Popping ConcurrentMethod1 (Thread: {Environment.CurrentManagedThreadId})");
            _callStack.Pop(methodInfo1);
            _reportOutputHelper.ProcessCallStackItem(new MethodCallEnd(methodInfo1));
        });

        var task2 = Task.Run(() =>
        {
            _logger.LogInformation($"Pushing ConcurrentMethod2 (Thread: {Environment.CurrentManagedThreadId})");
            _callStack.Push(methodInfo2);
            _reportOutputHelper.ProcessCallStackItem(new MethodCallStart(methodInfo2));
            Thread.Sleep(5);
            _logger.LogInformation($"Popping ConcurrentMethod2 (Thread: {Environment.CurrentManagedThreadId})");
            _callStack.Pop(methodInfo2);
            _reportOutputHelper.ProcessCallStackItem(new MethodCallEnd(methodInfo2));
        });

        await Task.WhenAll(task1, task2);

        _logger.LogInformation("Concurrent method execution completed");

        var reportItems = _reportOutputHelper.ReportItems.ToList();

        _logger.LogInformation($"Report items count: {reportItems.Count}");
        foreach (var item in reportItems)
        {
            _logger.LogInformation($"Method: {item.MethodName}, StartTime: {item.StartTime}, EndTime: {item.EndTime}");
        }

        Assert.That(reportItems, Has.Some.Matches<ReportItem>(item =>
            (item.MethodName ?? string.Empty).StartsWith("ConcurrentMethod1") && !string.IsNullOrEmpty(item.EndTime)));
        Assert.That(reportItems, Has.Some.Matches<ReportItem>(item =>
            (item.MethodName ?? string.Empty).StartsWith("ConcurrentMethod2") && !string.IsNullOrEmpty(item.EndTime)));
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
            _logger.LogInformation($"Processing: {item.GetType().Name} for {(item as IMethodLifeCycleItem)?.MethodCallInfo.MethodName}");
            var reportItem = _reportOutputHelper.ProcessCallStackItem(item);
            if (reportItem is not null)
            {
                _logger.LogInformation($"Added report item: {reportItem.MethodName}, StartTime: {reportItem.StartTime}, EndTime: {reportItem.EndTime}");
            }
        }

        var reportItems = _reportOutputHelper.ReportItems.ToList();
        _logger.LogInformation($"Total report items after processing: {reportItems.Count}");
        foreach (var item in reportItems)
        {
            _logger.LogInformation($"{item.MethodName}: StartTime={item.StartTime}, EndTime={item.EndTime}");
        }

        return reportItems;
    }
}

#pragma warning disable CL0002
namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using Reporters.ReportOutputs;
using Reporters;
using System;
using System.Linq;
using System.Collections.Generic;
using Moq;
using Microsoft.Extensions.Logging;
using System.Globalization;
using Core.Abstractions;
using Core.Controllers;
using Core.MethodLifecycle;
using Core.Models;
using Core.Pooling;
using TestUtilities.Logging;
using TestUtilities.TestHelpers;

[TestFixture]
public class ReportOutputHelperTests
{
    private TestLogger<ReportOutputHelperTests> _logger;
    private TestLoggerFactory<ReportOutputHelperTests> _loggerFactory;
    private ReportOutputHelper _reportOutputHelper;
    private Mock<IMethodCallReporter> _mockReporter;
    private IMonitoringController _monitoringController;
    private MethodCallInfoPool _methodCallInfoPool;

    [SetUp]
    public void Setup()
    {
        _logger = new TestLogger<ReportOutputHelperTests>();
        _loggerFactory = new TestLoggerFactory<ReportOutputHelperTests>(_logger);
        _loggerFactory.EnableLoggingFor<ReportOutputHelper>();
        _monitoringController = new MonitoringController(_loggerFactory);
        _methodCallInfoPool = new MethodCallInfoPool(_monitoringController, _loggerFactory);

        _reportOutputHelper = new ReportOutputHelper(_loggerFactory, new ReportItemFactory(_loggerFactory));
        _mockReporter = new Mock<IMethodCallReporter>();
        _mockReporter.Setup(r => r.FullName).Returns("TestReporter");

        _reportOutputHelper.Initialize(_mockReporter.Object);
    }

    [Test]
    public void Initialize_ShouldClearPreviousData()
    {
        // Arrange
        var initialItem = CreateTestMethodLifeCycleItem("InitialMethod");
        _reportOutputHelper.ProcessCallStackItem(initialItem);

        // Act
        _reportOutputHelper.Initialize(_mockReporter.Object);

        // Assert
        Assert.That(_reportOutputHelper.ReportItems, Is.Empty);
        Assert.That(_reportOutputHelper.Gaps, Is.Empty);
        Assert.That(_reportOutputHelper.ParameterNames, Is.Empty);
        Assert.That(_reportOutputHelper.LastEndTime, Is.Null);
    }

    [Test]
    public void ProcessCallStackItem_ShouldHandleMethodCallStart()
    {
        // Arrange
        var startItem = CreateTestMethodLifeCycleItem("TestMethod");

        // Act
        var result = _reportOutputHelper.ProcessCallStackItem(startItem);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.MethodName, Is.EqualTo("TestMethod"));
        Assert.That(result.StartTime, Is.Not.Null);
        Assert.That(_reportOutputHelper.ReportItems, Has.Count.EqualTo(1));

        // Additional logging for debugging
        if (result.MethodName != "TestMethod")
        {
            _logger.LogError($"Expected MethodName to be 'TestMethod', but was '{result.MethodName}'");
            _logger.LogError($"Full result object: {System.Text.Json.JsonSerializer.Serialize(result)}");
        }
    }

    [Test]
    public void ProcessCallStackItem_ShouldHandleMethodCallEnd()
    {
        // Arrange
        var methodId = Guid.NewGuid().ToString();
        var methodInfo = CreateMethodCallInfo("TestMethod", methodId);
        var startItem = new MethodCallStart(methodInfo);
        var endItem = new MethodCallEnd(methodInfo);

        // Act
        var startResult = _reportOutputHelper.ProcessCallStackItem(startItem);
        _logger.LogInformation($"Start result: {startResult?.MethodName}, Id: {startResult?.Id}");

        var endResult = _reportOutputHelper.ProcessCallStackItem(endItem);
        _logger.LogInformation($"End result: {endResult?.MethodName}, Id: {endResult?.Id}");

        // Assert
        Assert.That(startResult, Is.Not.Null, "Start result should not be null");
        Assert.That(endResult, Is.Not.Null, "End result should not be null");

        Assert.That(endResult.MethodName, Is.EqualTo("TestMethod"), "Method name should match");
        Assert.That(endResult.EndTime, Is.Not.Null, "End time should be set");
        Assert.That(endResult.Duration, Is.Not.Null, "Duration should be set");
        Assert.That(_reportOutputHelper.LastEndTime, Is.EqualTo(endResult.EndTime), "Last end time should be updated");

        // Log the current state of ReportItems
        _logger.LogInformation($"Report items count: {_reportOutputHelper.ReportItems.Count}");
        foreach (var item in _reportOutputHelper.ReportItems)
        {
            _logger.LogInformation($"Report item: {item.MethodName}, Id: {item.Id}, Start: {item.StartTime}, End: {item.EndTime}");
        }

        // Additional logging
        _logger.LogInformation($"MethodCallInfo Id: {methodInfo.Id}");
        _logger.LogInformation($"ReportOutputHelper LastEndTime: {_reportOutputHelper.LastEndTime}");
    }

    [Test]
    public void ProcessCallStackItem_ShouldHandleCallGap()
    {
        // Arrange
        var gapStart = DateTime.Now;
        var gapDuration = TimeSpan.FromSeconds(1);
        var callGap = new CallGap(gapStart, gapStart + gapDuration);

        // Act
        var result = _reportOutputHelper.ProcessCallStackItem(callGap);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.MethodName, Is.EqualTo(MethodCallParameter.Types.Gap));
        Assert.That(_reportOutputHelper.Gaps, Has.Count.EqualTo(1));
        Assert.That(_reportOutputHelper.Gaps[0].Duration, Is.EqualTo(gapDuration.TotalMilliseconds.ToString("N1", CultureInfo.InvariantCulture)));
    }

    [Test]
    public void ProcessCallStackItem_ShouldHandleUnknownItemType()
    {
        // Arrange
        var unknownItem = new Mock<ICallStackItem>().Object;

        // Act
        var result = _reportOutputHelper.ProcessCallStackItem(unknownItem);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public void AddReportItem_ShouldAddNewItemCorrectly()
    {
        // Arrange
        var newItem = new ReportItem { Id = "1", MethodName = "NewMethod" };

        // Act
        _reportOutputHelper.AddReportItem(newItem);

        // Assert
        var reportItems = _reportOutputHelper.ReportItems.ToArray();
        Assert.That(reportItems, Has.Length.EqualTo(1));
        Assert.That(reportItems[0].MethodName, Is.EqualTo("NewMethod"));
    }

    [Test]
    public void AddReportItem_ShouldUpdateExistingItem()
    {
        // Arrange
        var existingItem = new ReportItem { Id = "1", MethodName = "ExistingMethod", StartTime = "2023-01-01 00:00:00" };
        _reportOutputHelper.AddReportItem(existingItem);

        var updatedItem = new ReportItem { Id = "1", MethodName = "ExistingMethod", EndTime = "2023-01-01 00:01:00", Duration = "60000" };

        // Act
        _reportOutputHelper.AddReportItem(updatedItem);

        // Assert
        var reportItems = _reportOutputHelper.ReportItems.ToArray();
        Assert.That(reportItems, Has.Length.EqualTo(1));
        Assert.That(reportItems[0].EndTime, Is.EqualTo("2023-01-01 00:01:00"));
        Assert.That(reportItems[0].Duration, Is.EqualTo("60000"));
    }

    [Test]
    public void SetLimitOptions_ShouldApplyLimits()
    {
        // Arrange
        for (int i = 0; i < 10; i++)
        {
            _reportOutputHelper.AddReportItem(new ReportItem { Id = i.ToString(), MethodName = $"Method{i}" });
        }

        // Act
        _reportOutputHelper.SetLimitOptions(OutputLimitOptions.LimitItems(5));

        // Assert
        Assert.That(_reportOutputHelper.ReportItems, Has.Count.EqualTo(5));
        Assert.That(_reportOutputHelper.GetLimitOptions().MaxItems, Is.EqualTo(5));
    }

    [Test]
    public void GetDebugInfo_ShouldReturnCorrectInformation()
    {
        // Arrange
        _reportOutputHelper.AddReportItem(new ReportItem { Id = "1", MethodName = "TestMethod" });
        _reportOutputHelper.ProcessCallStackItem(new CallGap(DateTime.Now, DateTime.Now.AddSeconds(1)));
        _reportOutputHelper.SetLimitOptions(OutputLimitOptions.LimitItems(100));

        // Act
        var debugInfo = _reportOutputHelper.GetDebugInfo();

        // Assert
        _logger.LogInformation($"Debug Info: {debugInfo}");
        Assert.That(debugInfo, Does.Contain("ReportItems: 2"), "Should contain 2 ReportItems (1 regular + 1 gap)");
        Assert.That(debugInfo, Does.Contain("Gaps: 1"), "Should contain 1 Gap");
        Assert.That(debugInfo, Does.Contain("MaxItems=100"), "Should contain correct MaxItems value");

        // Log additional information
        _logger.LogInformation($"ReportItems count: {_reportOutputHelper.ReportItems.Count}");
        _logger.LogInformation($"Gaps count: {_reportOutputHelper.Gaps.Count}");
        foreach (var item in _reportOutputHelper.ReportItems)
        {
            _logger.LogInformation($"ReportItem: Id={item.Id}, MethodName={item.MethodName}, IsGap={item.MethodName == MethodCallParameter.Types.Gap}");
        }
    }

    private ICallStackItem CreateTestMethodLifeCycleItem(string methodName)
    {
        var methodInfo = CreateMethodCallInfo(methodName);
        return new MethodCallStart(methodInfo);
    }

    private MethodCallInfo CreateMethodCallInfo(string methodName, string? id = null)
    {
        var testMethod = new TestMethodInfo(methodName, typeof(ReportOutputHelperTests));
        var methodCallInfo = _methodCallInfoPool.Rent(
            null,
            typeof(ReportOutputHelperTests),
            testMethod,
            Array.Empty<Type>(),
            id ?? Guid.NewGuid().ToString(),
            new Dictionary<string, string>()
        );

        // Ensure MethodName is set
        methodCallInfo.MethodName = methodName;

        return methodCallInfo;
    }
}

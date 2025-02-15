﻿#pragma warning disable CL0002
namespace Orc.Monitoring.Tests;

using Moq;
using NUnit.Framework;
using MethodLifeCycleItems;
using Reporters.ReportOutputs;
using Reporters;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TestUtilities;
using TestUtilities.Logging;
using TestUtilities.Mocks;
using TestUtilities.TestHelpers;
using Microsoft.Extensions.Logging;

[TestFixture]
public class RanttOutputLimitableTests
{
    private TestLogger<RanttOutputLimitableTests> _logger;
    private TestLoggerFactory<RanttOutputLimitableTests> _loggerFactory;
    private MethodCallInfoPool _methodCallInfoPool;
    private IMonitoringController _monitoringController;
    private RanttOutput _ranttOutput;
    private string _testOutputPath;
    private InMemoryFileSystem _fileSystem;
    private ReportArchiver _reportArchiver;
    private CsvUtils _csvUtils;

    [SetUp]
    public void Setup()
    {
        _logger = new TestLogger<RanttOutputLimitableTests>();
        _loggerFactory = new TestLoggerFactory<RanttOutputLimitableTests>(_logger);
        _loggerFactory.EnableLoggingFor<ReportOutputHelper>();
        _loggerFactory.EnableLoggingFor<RanttOutput>();
        _fileSystem = new InMemoryFileSystem(_loggerFactory);
        _csvUtils = TestHelperMethods.CreateCsvUtils(_fileSystem, _loggerFactory);
        _reportArchiver = new ReportArchiver(_fileSystem, _loggerFactory);
        _monitoringController = new MonitoringController(_loggerFactory);
        _methodCallInfoPool = new MethodCallInfoPool(_monitoringController, _loggerFactory);

        _testOutputPath = _fileSystem.Combine(_fileSystem.GetTempPath(), _fileSystem.GetRandomFileName());
        _fileSystem.CreateDirectory(_testOutputPath);
        _ranttOutput = new RanttOutput(_loggerFactory,
            () => new EnhancedDataPostProcessor(_loggerFactory),
            new ReportOutputHelper(_loggerFactory, new ReportItemFactory(_loggerFactory)),
            (outputDirectory) => new MethodOverrideManager(outputDirectory, _loggerFactory, _fileSystem, _csvUtils),
            _fileSystem,
            _reportArchiver, new ReportItemFactory(_loggerFactory), _csvUtils);
        var parameters = RanttOutput.CreateParameters(_testOutputPath);
        _ranttOutput.SetParameters(parameters);

        _monitoringController.Enable();
    }

    [TearDown]
    public void TearDown()
    {
        _fileSystem.Dispose();
        if (_fileSystem.DirectoryExists(_testOutputPath))
        {
            _fileSystem.DeleteDirectory(_testOutputPath, true);
        }
    }

    [Test]
    public void SetLimitOptions_SetsOptionsCorrectly()
    {
        var options = OutputLimitOptions.LimitItems(TestConstants.DefaultTestMaxItems);
        _ranttOutput.SetLimitOptions(options);
        var retrievedOptions = _ranttOutput.GetLimitOptions();

        Assert.That(retrievedOptions.MaxItems, Is.EqualTo(options.MaxItems));
    }

    [Test]
    public void GetLimitOptions_ReturnsDefaultOptionsInitially()
    {
        var options = _ranttOutput.GetLimitOptions();
        Assert.That(options.MaxItems, Is.Null);
    }

    [Test]
    public async Task WriteItem_RespectsItemCountLimit()
    {
        _ranttOutput.SetLimitOptions(OutputLimitOptions.LimitItems(TestConstants.DefaultTestMaxItems));
        var mockReporter = new Mock<IMethodCallReporter>();
        mockReporter.Setup(r => r.FullName).Returns(TestConstants.DefaultTestReporterName);
        await using (var _ = _ranttOutput.Initialize(mockReporter.Object))
        {
            for (int i = 0; i < TestConstants.DefaultTestMaxItems * 2; i++)
            {
                var item = CreateTestMethodLifeCycleItem($"{TestConstants.DefaultTestMethodName}{i}", TestConstants.DefaultItemStartTime.AddMinutes(-i));
                _ranttOutput.WriteItem(item);
            }
        }

        var filePath = _fileSystem.Combine(_testOutputPath, TestConstants.DefaultTestReporterName, TestConstants.DefaultCsvFileName);
        Assert.That(_fileSystem.FileExists(filePath), Is.True, "CSV file should be created");

        var fileContent = await _fileSystem.ReadAllTextAsync(filePath);
        _logger.LogInformation($"File content:\n{fileContent}");
        var lines = fileContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        _logger.LogInformation($"Number of non-empty lines: {lines.Length}");

        Assert.That(lines.Length, Is.EqualTo(TestConstants.DefaultTestMaxItems + 1), "Should have header and limited number of data lines");

        // Log each line for debugging
        for (int i = 0; i < lines.Length; i++)
        {
            _logger.LogInformation($"Line {i}: {lines[i]}");
        }

        Assert.That(lines[0], Does.Contain("Id"), "First line should be the header");
        Assert.That(lines.Skip(1).Count(), Is.EqualTo(TestConstants.DefaultTestMaxItems), $"Should have {TestConstants.DefaultTestMaxItems} data lines");
    }

    [Test]
    public async Task WriteItem_WithNoLimit_WritesAllItems()
    {
        var mockReporter = new Mock<IMethodCallReporter>();
        mockReporter.Setup(r => r.FullName).Returns(TestConstants.DefaultTestReporterName);
        await using (var _ = _ranttOutput.Initialize(mockReporter.Object))
        {
            for (int i = 0; i < TestConstants.DefaultItemCount; i++)
            {
                var item = CreateTestMethodLifeCycleItem($"{TestConstants.DefaultTestMethodName}{i}", TestConstants.DefaultItemStartTime.AddMinutes(-i));
                _ranttOutput.WriteItem(item);
            }
        }

        var filePath = _fileSystem.Combine(_testOutputPath, TestConstants.DefaultTestReporterName, TestConstants.DefaultCsvFileName);
        var lines = await _fileSystem.ReadAllLinesAsync(filePath);

        _logger.LogInformation($"File content:\n{string.Join("\n", lines)}");

        Assert.That(lines.Length, Is.EqualTo(TestConstants.DefaultItemCount + 1), $"Expected {TestConstants.DefaultItemCount + 1} lines (header + {TestConstants.DefaultItemCount} items)");
        Assert.That(lines[0], Does.Contain("Id"), "First line should be the header");
        Assert.That(lines.Skip(1).Count(), Is.EqualTo(TestConstants.DefaultItemCount), $"Should have {TestConstants.DefaultItemCount} items");
    }

    private ICallStackItem CreateTestMethodLifeCycleItem(string itemName, DateTime timestamp)
    {
        var methodInfo = new TestMethodInfo(itemName, typeof(RanttOutputLimitableTests));
        var methodCallInfo = _methodCallInfoPool.Rent(
            null,
            typeof(RanttOutputLimitableTests),
            methodInfo,
            TestConstants.EmptyTypeArray,
            Guid.NewGuid().ToString(),
            new Dictionary<string, string>()
        );
        methodCallInfo.StartTime = timestamp;

        var mockMethodLifeCycleItem = new Mock<MethodCallStart>(methodCallInfo);
        return mockMethodLifeCycleItem.Object;
    }
}

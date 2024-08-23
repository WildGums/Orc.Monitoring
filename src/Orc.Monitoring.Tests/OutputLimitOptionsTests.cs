#pragma warning disable CL0002
namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using Orc.Monitoring.Reporters;
using Orc.Monitoring.Reporters.ReportOutputs;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Orc.Monitoring.MethodLifeCycleItems;
using System.Linq;
using Microsoft.Extensions.Logging;
using System.Reflection;

[TestFixture]
public class OutputLimitOptionsTests
{
    private string _testOutputPath;
    private MethodCallInfoPool _methodCallInfoPool;
    private ILogger<OutputLimitOptionsTests> _logger;

    [SetUp]
    public void Setup()
    {
        _testOutputPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_testOutputPath);
        _methodCallInfoPool = new MethodCallInfoPool();
        _logger = MonitoringController.CreateLogger<OutputLimitOptionsTests>();
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_testOutputPath))
        {
            Directory.Delete(_testOutputPath, true);
        }
    }

    [Test]
    public async Task CsvReportOutput_AppliesItemCountLimit()
    {
        // Arrange
        var csvOutput = new CsvReportOutput();
        var parameters = CsvReportOutput.CreateParameters(_testOutputPath, "TestReport", OutputLimitOptions.LimitItems(5));
        csvOutput.SetParameters(parameters);

        var mockReporter = new Mock<IMethodCallReporter>();
        mockReporter.Setup(r => r.FullName).Returns("TestReporter");

        // Act
        await using (var disposable = csvOutput.Initialize(mockReporter.Object))
        {
            for (int i = 0; i < 10; i++)
            {
                var item = CreateTestMethodLifeCycleItem($"Item{i}", DateTime.Now.AddMinutes(i));
                _logger.LogDebug($"Writing item: {item}");
                csvOutput.WriteItem(item);
                _logger.LogDebug($"After writing item {i}, ReportItems count: {((CsvReportOutput)csvOutput).GetReportItemsCount()}");
            }

            _logger.LogInformation($"Debug info before disposing: {((CsvReportOutput)csvOutput).GetDebugInfo()}");
        }

        _logger.LogInformation($"Debug info after disposing: {((CsvReportOutput)csvOutput).GetDebugInfo()}");

        // Assert
        var outputFile = await WaitForFileCreationAsync(Path.Combine(_testOutputPath, "TestReport.csv"));
        Assert.That(outputFile, Is.Not.Null, $"CSV file should be created in {_testOutputPath}");

        if (outputFile is not null)
        {
            await Task.Delay(100); // Add a small delay to ensure file is fully written and closed

            var lines = await File.ReadAllLinesAsync(outputFile);
            _logger.LogInformation($"CSV file content ({lines.Length} lines):");
            foreach (var line in lines)
            {
                _logger.LogInformation(line);
            }

            Assert.That(lines.Length, Is.EqualTo(6), "CSV should contain header + 5 items");
            Assert.That(lines[^1], Does.Contain("Item9"), "Last item should be the most recent one");
        }
    }

    [Test]
    public async Task RanttOutput_AppliesAgeLimit()
    {
        // Arrange
        var ranttOutput = new RanttOutput();
        var parameters = RanttOutput.CreateParameters(_testOutputPath, OutputLimitOptions.LimitAge(TimeSpan.FromMinutes(5)));
        ranttOutput.SetParameters(parameters);

        var mockReporter = new Mock<IMethodCallReporter>();
        mockReporter.Setup(r => r.FullName).Returns("TestReporter");

        // Act
        await using (var disposable = ranttOutput.Initialize(mockReporter.Object))
        {
            for (int i = 0; i < 5; i++)
            {
                var timestamp = DateTime.Now.AddMinutes(-i * 2);
                var item = CreateTestMethodLifeCycleItem($"Item{i}", timestamp);
                _logger.LogDebug($"Writing item: {item} at {timestamp}");
                ranttOutput.WriteItem(item);
            }

            _logger.LogInformation($"Debug info before disposing: {((RanttOutput)ranttOutput).GetDebugInfo()}");
        }

        _logger.LogInformation($"Debug info after disposing: {((RanttOutput)ranttOutput).GetDebugInfo()}");

        // Assert
        var outputFile = await WaitForFileCreationAsync(Path.Combine(_testOutputPath, "TestReporter", "TestReporter.csv"));
        Assert.That(outputFile, Is.Not.Null, $"CSV file should be created in {_testOutputPath}");

        if (outputFile is not null)
        {
            var lines = await File.ReadAllLinesAsync(outputFile);
            _logger.LogInformation($"CSV file content ({lines.Length} lines):");
            foreach (var line in lines)
            {
                _logger.LogInformation(line);
            }

            Assert.That(lines.Length, Is.EqualTo(4), "CSV should contain header + 3 items (within 5 minutes)");
        }
    }

    [Test]
    public async Task TxtReportOutput_AppliesBothLimits()
    {
        // Arrange
        var txtOutput = new TxtReportOutput();
        var parameters = TxtReportOutput.CreateParameters(_testOutputPath, "TestDisplay", OutputLimitOptions.Limit(3, TimeSpan.FromMinutes(2)));
        txtOutput.SetParameters(parameters);

        var mockReporter = new Mock<IMethodCallReporter>();
        mockReporter.Setup(r => r.FullName).Returns("TestReporter");
        mockReporter.Setup(r => r.RootMethod).Returns((MethodInfo)null);

        // Act
        await using (var disposable = txtOutput.Initialize(mockReporter.Object))
        {
            for (int i = 0; i < 5; i++)
            {
                var timestamp = DateTime.Now.AddMinutes(-i);
                var item = CreateTestMethodLifeCycleItem($"Item{i}", timestamp);
                _logger.LogDebug($"Writing item: {item} at {timestamp}");
                txtOutput.WriteItem(item);
            }

            _logger.LogInformation($"Debug info before disposing: {((TxtReportOutput)txtOutput).GetDebugInfo()}");
        }

        _logger.LogInformation($"Debug info after disposing: {((TxtReportOutput)txtOutput).GetDebugInfo()}");

        // Assert
        var outputFile = await WaitForFileCreationAsync(Path.Combine(_testOutputPath, "TestReporter_TestDisplay.txt"));
        Assert.That(outputFile, Is.Not.Null, $"TXT file should be created in {_testOutputPath}");

        if (outputFile is not null)
        {
            var lines = await File.ReadAllLinesAsync(outputFile);
            _logger.LogInformation($"TXT file content ({lines.Length} lines):");
            foreach (var line in lines)
            {
                _logger.LogInformation(line);
            }

            Assert.That(lines.Length, Is.EqualTo(2), "TXT should contain 2 items (limited by count and age)");
            Assert.That(lines[0], Does.Contain("Item0"));
            Assert.That(lines[1], Does.Contain("Item1"));
        }
    }

    private ICallStackItem CreateTestMethodLifeCycleItem(string itemName, DateTime? timestamp = null)
    {
        var methodInfo = new TestMethodInfo(itemName, GetType());
        var methodCallInfo = _methodCallInfoPool.Rent(null, GetType(), methodInfo, Array.Empty<Type>(), Guid.NewGuid().ToString(), new Dictionary<string, string>());

        if (timestamp.HasValue)
        {
            methodCallInfo.StartTime = timestamp.Value;
        }

        var mockMethodLifeCycleItem = new Mock<MethodCallStart>(methodCallInfo);
        _logger.LogDebug($"Created test method lifecycle item: {itemName}, StartTime: {methodCallInfo.StartTime}, Id: {methodCallInfo.Id}");

        return mockMethodLifeCycleItem.Object;
    }

    private async Task<string?> WaitForFileCreationAsync(string filePath, int maxAttempts = 60, int delayMs = 1000)
    {
        for (int i = 0; i < maxAttempts; i++)
        {
            if (File.Exists(filePath))
            {
                return filePath;
            }
            await Task.Delay(delayMs);
        }
        _logger.LogWarning($"File not created after {maxAttempts} attempts: {filePath}");
        return null;
    }
}

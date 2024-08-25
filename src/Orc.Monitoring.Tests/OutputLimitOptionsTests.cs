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
                var item = CreateTestMethodLifeCycleItem($"Item{i}", DateTime.Now.AddMinutes(-i));
                _logger.LogDebug($"Writing item: {item}, StartTime: {((MethodCallStart)item).MethodCallInfo.StartTime}");
                csvOutput.WriteItem(item);
                _logger.LogDebug($"After writing item {i}, ReportItems count: {((CsvReportOutput)csvOutput).GetReportItemsCount()}");
            }
            _logger.LogInformation($"Debug info before disposing: {((CsvReportOutput)csvOutput).GetDebugInfo()}");
        }

        _logger.LogInformation($"Debug info after disposing: {((CsvReportOutput)csvOutput).GetDebugInfo()}");

        // Assert
        var outputFile = Path.Combine(_testOutputPath, "TestReport.csv");
        Assert.That(File.Exists(outputFile), Is.True, $"CSV file should be created in {_testOutputPath}");

        if (File.Exists(outputFile))
        {
            var lines = await File.ReadAllLinesAsync(outputFile);
            var fileContent = await File.ReadAllTextAsync(outputFile);
            _logger.LogInformation($"CSV file content:\n{fileContent}");

            Assert.That(lines.Length, Is.EqualTo(6), "CSV should contain header + 5 items");

            for (int i = 0; i < 5; i++)
            {
                _logger.LogInformation($"Checking line {i + 1}: {lines[i + 1]}");
                Assert.That(lines[i + 1], Does.Contain($"Item{4 - i}"), $"Line {i + 1} should contain Item{4 - i}");
            }
        }
    }

    [Test]
    public async Task TxtReportOutput_AppliesBothLimits()
    {
        // Arrange
        var txtOutput = new TxtReportOutput();
        var displayName = "TestDisplay";
        var now = DateTime.Now;
        var parameters = TxtReportOutput.CreateParameters(_testOutputPath, displayName, OutputLimitOptions.Limit(3, TimeSpan.FromMinutes(2)));
        txtOutput.SetParameters(parameters);

        var mockReporter = new Mock<IMethodCallReporter>();
        mockReporter.Setup(r => r.FullName).Returns("TestReporter");
        mockReporter.Setup(r => r.Name).Returns("TestReporter");

        // Setup a mock root method with the display name attribute
        var mockMethod = new Mock<MethodInfo>();
        mockMethod.Setup(m => m.GetCustomAttributes(typeof(MethodCallParameterAttribute), false))
            .Returns(new[] { new MethodCallParameterAttribute(displayName, "TestDisplay") });
        mockReporter.Setup(r => r.RootMethod).Returns(mockMethod.Object);

        // Act
        _logger.LogInformation("Starting TxtReportOutput test");
        var disposable = txtOutput.Initialize(mockReporter.Object);
        _logger.LogInformation("TxtReportOutput initialized");

        // Add 5 items: 3 within time limit, 2 outside
        var items = new[]
        {
        CreateTestMethodLifeCycleItem("Item0", now.AddMinutes(-0.5)),
        CreateTestMethodLifeCycleItem("Item1", now.AddMinutes(-1.0)),
        CreateTestMethodLifeCycleItem("Item2", now.AddMinutes(-1.5)),
        CreateTestMethodLifeCycleItem("Item3", now.AddMinutes(-2.5)),
        CreateTestMethodLifeCycleItem("Item4", now.AddMinutes(-3.0))
    };

        foreach (var item in items)
        {
            var methodCallInfo = ((MethodCallStart)item).MethodCallInfo;
            _logger.LogInformation($"Writing item: {methodCallInfo.MethodName} at {methodCallInfo.StartTime}");
            txtOutput.WriteItem(item);
        }

        _logger.LogInformation($"Debug info before disposing: {((TxtReportOutput)txtOutput).GetDebugInfo()}");
        await disposable.DisposeAsync();
        _logger.LogInformation($"Debug info after disposing: {((TxtReportOutput)txtOutput).GetDebugInfo()}");

        // Assert
        var expectedFileName = $"{mockReporter.Object.Name}_TestDisplay.txt";
        var expectedOutputFile = Path.Combine(_testOutputPath, expectedFileName);
        _logger.LogInformation($"Expected file: {expectedOutputFile}");

        var allFiles = Directory.GetFiles(_testOutputPath);
        _logger.LogInformation($"All files in directory: {string.Join(", ", allFiles)}");

        Assert.That(File.Exists(expectedOutputFile), Is.True, $"Expected TXT file '{expectedFileName}' should be created in {_testOutputPath}");

        if (File.Exists(expectedOutputFile))
        {
            var fileContent = await File.ReadAllTextAsync(expectedOutputFile);
            _logger.LogInformation($"File content:\n{fileContent}");
            var lines = fileContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            _logger.LogInformation($"Number of lines: {lines.Length}");

            foreach (var line in lines)
            {
                _logger.LogInformation(line);
            }

            Assert.That(lines.Length, Is.EqualTo(3), "TXT should contain 3 items (limited by count and age)");
            Assert.That(lines[0], Does.Contain("Item0"));
            Assert.That(lines[1], Does.Contain("Item1"));
            Assert.That(lines[2], Does.Contain("Item2"));
        }
        else
        {
            _logger.LogError($"Expected file not found: {expectedOutputFile}");
            Assert.Fail($"Expected file not found: {expectedOutputFile}");
        }
    }

    [Test]
    public async Task TxtReportOutput_GeneratesCorrectFileName()
    {
        // Arrange
        var txtOutput = new TxtReportOutput();
        var displayName = "TestDisplayName";
        var parameters = TxtReportOutput.CreateParameters(_testOutputPath, displayName);
        txtOutput.SetParameters(parameters);

        var mockReporter = new Mock<IMethodCallReporter>();
        mockReporter.Setup(r => r.FullName).Returns("TestReporter");
        mockReporter.Setup(r => r.Name).Returns("TestReporterName");

        // Setup a mock root method with the display name attribute
        var mockMethod = new Mock<MethodInfo>();
        mockMethod.Setup(m => m.GetCustomAttributes(typeof(MethodCallParameterAttribute), false))
            .Returns(new[] { new MethodCallParameterAttribute(displayName, "TestValue") });
        mockReporter.Setup(r => r.RootMethod).Returns(mockMethod.Object);

        // Act
        _logger.LogInformation("Starting TxtReportOutput file name generation test");
        var disposable = txtOutput.Initialize(mockReporter.Object);
        _logger.LogInformation("TxtReportOutput initialized");

        // Write a single item to ensure file creation
        var item = CreateTestMethodLifeCycleItem("TestItem", DateTime.Now);
        txtOutput.WriteItem(item);

        await disposable.DisposeAsync();

        // Assert
        var expectedFileName = $"TestReporterName_TestValue.txt";
        var expectedOutputFile = Path.Combine(_testOutputPath, expectedFileName);
        _logger.LogInformation($"Expected file: {expectedOutputFile}");

        var allFiles = Directory.GetFiles(_testOutputPath);
        _logger.LogInformation($"All files in directory: {string.Join(", ", allFiles)}");

        Assert.That(File.Exists(expectedOutputFile), Is.True, $"Expected TXT file '{expectedFileName}' should be created in {_testOutputPath}");

        if (!File.Exists(expectedOutputFile))
        {
            _logger.LogError($"Expected file not found: {expectedOutputFile}");
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
        var outputFile = Path.Combine(_testOutputPath, "TestReporter", "TestReporter.csv");
        Assert.That(File.Exists(outputFile), Is.True, $"CSV file should be created in {_testOutputPath}");

        if (File.Exists(outputFile))
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

    private ICallStackItem CreateTestMethodLifeCycleItem(string itemName, DateTime? timestamp = null)
    {
        var methodInfo = new TestMethodInfo(itemName, GetType());
        var methodCallInfo = _methodCallInfoPool.Rent(null, GetType(), methodInfo, Array.Empty<Type>(), Guid.NewGuid().ToString(), new Dictionary<string, string>());

        // Set the MethodName explicitly
        methodCallInfo.MethodName = itemName;

        if (timestamp.HasValue)
        {
            methodCallInfo.StartTime = timestamp.Value;
        }

        var mockMethodLifeCycleItem = new Mock<MethodCallStart>(methodCallInfo);
        _logger.LogDebug($"Created test method lifecycle item: {itemName}, StartTime: {methodCallInfo.StartTime}, Id: {methodCallInfo.Id}");
        return mockMethodLifeCycleItem.Object;
    }
}

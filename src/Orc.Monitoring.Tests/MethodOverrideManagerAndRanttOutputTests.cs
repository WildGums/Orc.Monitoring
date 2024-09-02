namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using Orc.Monitoring.Reporters.ReportOutputs;
using Orc.Monitoring.Reporters;
using Orc.Monitoring.MethodLifeCycleItems;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Reflection;
using Microsoft.Extensions.Logging;

#pragma warning disable CL0002


[TestFixture]
public class MethodOverrideManagerAndRanttOutputTests
{
    private string _testOutputPath;
    private string _overrideFilePath;
    private string _overrideTemplateFilePath;
    private TestLogger<MethodOverrideManagerAndRanttOutputTests> _logger;
    private TestLoggerFactory<MethodOverrideManagerAndRanttOutputTests> _loggerFactory;
    private IMonitoringController _monitoringController;
    private MethodCallInfoPool _methodCallInfoPool;
    private InMemoryFileSystem _fileSystem;
    private ReportArchiver _reportArchiver;
    private CsvUtils _csvUtils;

    [SetUp]
    public void Setup()
    {
        _fileSystem = new InMemoryFileSystem();
        _csvUtils = new CsvUtils(_fileSystem);
        _reportArchiver = new ReportArchiver(_fileSystem);
        _testOutputPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        _fileSystem.CreateDirectory(_testOutputPath);

        _overrideFilePath = Path.Combine(_testOutputPath, "method_overrides.csv");
        _overrideTemplateFilePath = Path.Combine(_testOutputPath, "method_overrides.template");
        _logger = new TestLogger<MethodOverrideManagerAndRanttOutputTests>();
        _loggerFactory = new TestLoggerFactory<MethodOverrideManagerAndRanttOutputTests>(_logger);
        _monitoringController = new MonitoringController(_loggerFactory, () => new EnhancedDataPostProcessor(_loggerFactory));
        _methodCallInfoPool = new MethodCallInfoPool(_monitoringController, _loggerFactory);

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
    public void SaveOverrides_WithDuplicateCustomColumns_ShouldNotProduceDuplicateColumnsInTemplate()
    {
        var manager = new MethodOverrideManager(_testOutputPath, _loggerFactory, _fileSystem, _csvUtils);
        var parameters = new Dictionary<string, string>()
        {
            { "CustomColumn", "Value1" },
            { "customcolumn", "Value2" } // This will overwrite the previous value due to case-insensitive dictionary
        };

        var reportItems = new List<ReportItem>
        {
            new ReportItem
            {
                FullName = "Test.Method",
                Parameters = parameters,
                AttributeParameters = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "CustomColumn" }
            }
        };

        manager.SaveOverrides(reportItems);

        var templateContent = _fileSystem.ReadAllText(_overrideTemplateFilePath);
        var headers = templateContent.Split('\n')[0].Split(',').Select(h => h.Trim()).ToArray();
        var uniqueHeaders = new HashSet<string>(headers, StringComparer.OrdinalIgnoreCase);

        Assert.That(headers.Length, Is.EqualTo(uniqueHeaders.Count), "Template should not contain duplicate columns");
        Assert.That(uniqueHeaders, Does.Contain("CustomColumn"), "CustomColumn should be present");
    }

    [Test]
    public void SaveOverrides_MultipleSaves_ShouldNotDuplicateColumnsInTemplate()
    {
        var manager = new MethodOverrideManager(_testOutputPath, _loggerFactory, _fileSystem, _csvUtils);
        var reportItems1 = new List<ReportItem>
        {
            new ReportItem
            {
                FullName = "Test.Method1",
                Parameters = new Dictionary<string, string> { { "CustomColumn1", "Value1" } },
                AttributeParameters = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "CustomColumn1" }
            }
        };
        var reportItems2 = new List<ReportItem>
        {
            new ReportItem
            {
                FullName = "Test.Method2",
                Parameters = new Dictionary<string, string> { { "CustomColumn2", "Value2" } },
                AttributeParameters = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "CustomColumn2" }
            }
        };

        manager.SaveOverrides(reportItems1);
        var templateContent1 = _fileSystem.ReadAllText(_overrideTemplateFilePath);
        _logger.LogInformation($"Template content after first save: {templateContent1}");

        manager.SaveOverrides(reportItems2);
        var templateContent2 = _fileSystem.ReadAllText(_overrideTemplateFilePath);
        _logger.LogInformation($"Template content after second save: {templateContent2}");

        var headers = templateContent2.Split('\n')[0].Split(',').Select(h => h.Trim()).ToArray();
        var uniqueHeaders = new HashSet<string>(headers, StringComparer.OrdinalIgnoreCase);

        Assert.That(headers.Length, Is.EqualTo(uniqueHeaders.Count), "Template should not contain duplicate columns after multiple saves");
        Assert.That(uniqueHeaders, Does.Contain("CustomColumn1"), "CustomColumn1 should be present");
        Assert.That(uniqueHeaders, Does.Contain("CustomColumn2"), "CustomColumn2 should be present");
    }

    [Test]
    public async Task RanttOutput_GenerateReport_ShouldProduceValidRanttFile()
    {
        var ranttOutput = CreateRanttOutput();
        var parameters = RanttOutput.CreateParameters(_testOutputPath);
        ranttOutput.SetParameters(parameters);

        var mockReporter = new Mock<IMethodCallReporter>();
        mockReporter.Setup(r => r.FullName).Returns("TestReporter");

        await using (var disposable = ranttOutput.Initialize(mockReporter.Object))
        {
            // Simulate writing items
            for (int i = 0; i < 100; i++)
            {
                var item = CreateTestMethodLifeCycleItem($"Item{i}", DateTime.Now.AddMinutes(-i));
                ranttOutput.WriteItem(item);
            }
        }

        var ranttProjectFile = Path.Combine(_testOutputPath, "TestReporter", "TestReporter.rprjx");
        Assert.That(_fileSystem.FileExists(ranttProjectFile), Is.True, "Rantt project file should be created");

        // Verify Rantt file integrity
        VerifyRanttFileIntegrity(ranttProjectFile);
    }

    [Test]
    public async Task RanttOutput_WithOverrides_ShouldUseOverridesFromCsvFile()
    {
        // Arrange
        var csvContent = "FullName,CustomColumn\nMethodOverrideManagerAndRanttOutputTests.Test.Method,OverrideValue";
        await _fileSystem.WriteAllTextAsync(_overrideFilePath, csvContent);
        _logger.LogInformation($"Override file content: {csvContent}");

        var ranttOutput = CreateRanttOutput();
        var parameters = RanttOutput.CreateParameters(_testOutputPath);
        ranttOutput.SetParameters(parameters);

        var mockReporter = new Mock<IMethodCallReporter>();
        mockReporter.Setup(r => r.FullName).Returns("TestReporter");

        // Act
        await using (var disposable = ranttOutput.Initialize(mockReporter.Object))
        {
            var item = CreateTestMethodLifeCycleItem("Test.Method", DateTime.Now);
            ranttOutput.WriteItem(item);
        }

        // Assert
        var csvOutputPath = Path.Combine(_testOutputPath, "TestReporter", "TestReporter.csv");
        Assert.That(_fileSystem.FileExists(csvOutputPath), Is.True, "CSV output file should be created");

        var csvOutputContent = await _fileSystem.ReadAllTextAsync(csvOutputPath);
        _logger.LogInformation($"CSV output content: {csvOutputContent}");

        Assert.That(csvOutputContent, Does.Contain("OverrideValue"), "CSV output should contain the override value");
    }

    private ICallStackItem CreateTestMethodLifeCycleItem(string methodName, DateTime timestamp)
    {
        var methodInfo = new TestMethodInfo(methodName, typeof(MethodOverrideManagerAndRanttOutputTests));
        var methodCallInfo = _methodCallInfoPool.Rent(
            null,
            typeof(MethodOverrideManagerAndRanttOutputTests),
            methodInfo,
            Array.Empty<Type>(),
            Guid.NewGuid().ToString(),
            new Dictionary<string, string>
            {
                ["FullName"] = $"MethodOverrideManagerAndRanttOutputTests.{methodName}"
            }
        );
        methodCallInfo.StartTime = timestamp;

        var mockMethodLifeCycleItem = new Mock<MethodCallStart>(methodCallInfo);
        return mockMethodLifeCycleItem.Object;
    }


    [Test]
    public async Task RanttOutput_GenerateReport_ShouldUpdateTemplateFile()
    {
        // Arrange
        var ranttOutput = CreateRanttOutput();
        var parameters = RanttOutput.CreateParameters(_testOutputPath);
        ranttOutput.SetParameters(parameters);

        var mockReporter = new Mock<IMethodCallReporter>();
        mockReporter.Setup(r => r.FullName).Returns("TestReporter");

        // Act
        await using (var disposable = ranttOutput.Initialize(mockReporter.Object))
        {
            var item = CreateTestMethodLifeCycleItem("Test.Method", DateTime.Now, new Dictionary<string, string> { { "CustomColumn", "CustomValue" } });
            ranttOutput.WriteItem(item);
        }

        // Assert
        Assert.That(_fileSystem.FileExists(_overrideTemplateFilePath), Is.True, "Template file should be created");
        var templateContent = await _fileSystem.ReadAllTextAsync(_overrideTemplateFilePath);
        Assert.That(templateContent, Does.Contain("CustomColumn"), "Template should contain the custom column");
        Assert.That(templateContent, Does.Contain("CustomValue"), "Template should contain the custom value");
    }

    private ICallStackItem CreateTestMethodLifeCycleItem(string itemName, DateTime timestamp, Dictionary<string, string>? parameters = null)
    {
        var methodInfo = new TestMethodInfo(itemName, typeof(MethodOverrideManagerAndRanttOutputTests));
        var methodCallInfo = _methodCallInfoPool.Rent(
            null,
            typeof(MethodOverrideManagerAndRanttOutputTests),
            methodInfo,
            Array.Empty<Type>(),
            Guid.NewGuid().ToString(),
            parameters ?? new Dictionary<string, string>()
        );
        methodCallInfo.StartTime = timestamp;

        var mockMethodLifeCycleItem = new Mock<MethodCallStart>(methodCallInfo);
        return mockMethodLifeCycleItem.Object;
    }

    private void VerifyRanttFileIntegrity(string ranttProjectFile)
    {
        // Read the Rantt project file
        var projectContent = _fileSystem.ReadAllText(ranttProjectFile);

        // Perform basic checks on the file content
        Assert.That(projectContent, Does.Contain("<Project RanttVersion="), "Rantt project file should contain version information");
        Assert.That(projectContent, Does.Contain("<DataSets>"), "Rantt project file should contain DataSets section");
        Assert.That(projectContent, Does.Contain("<Operations"), "Rantt project file should contain Operations section");
        Assert.That(projectContent, Does.Contain("<Relationships"), "Rantt project file should contain Relationships section");

        // Verify referenced CSV files exist
        var csvFileName = projectContent.Split(new[] { "Source=\"" }, StringSplitOptions.None)[1].Split('"')[0];
        var csvFilePath = Path.Combine(Path.GetDirectoryName(ranttProjectFile), csvFileName);
        Assert.That(_fileSystem.FileExists(csvFilePath), Is.True, "Referenced CSV file should exist");

        // Check CSV file content
        var csvContent = _fileSystem.ReadAllText(csvFilePath);
        var csvLines = csvContent.Split('\n');
        Assert.That(csvLines.Length, Is.GreaterThan(1), "CSV file should contain data");

        var headers = csvLines[0].Split(',');
        Assert.That(headers, Does.Contain("MethodName"), "CSV should contain MethodName column");
        Assert.That(headers, Does.Contain("Id"), "CSV should contain Id column");

        // Check for duplicate columns in CSV
        var uniqueHeaders = new HashSet<string>(headers);
        Assert.That(headers.Length, Is.EqualTo(uniqueHeaders.Count), "CSV should not contain duplicate columns");
    }

    private RanttOutput CreateRanttOutput()
    {
        var ranttOutput = new RanttOutput(MonitoringLoggerFactory.Instance, 
            () => new EnhancedDataPostProcessor(MonitoringLoggerFactory.Instance),
            new ReportOutputHelper(_loggerFactory),
            (outputDirectory) => new MethodOverrideManager(outputDirectory, _loggerFactory, _fileSystem, _csvUtils),
            _fileSystem,
            _reportArchiver);
        var parameters = RanttOutput.CreateParameters(_testOutputPath);
        ranttOutput.SetParameters(parameters);
        return ranttOutput;
    }
}

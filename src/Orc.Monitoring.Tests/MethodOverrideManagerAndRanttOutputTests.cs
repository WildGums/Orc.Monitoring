#pragma warning disable CL0002
namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using Orc.Monitoring.Reporters.ReportOutputs;
using Orc.Monitoring.Reporters;
using Orc.Monitoring.MethodLifeCycleItems;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

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
#pragma warning disable IDISP006
    private InMemoryFileSystem _fileSystem;
#pragma warning restore IDISP006
    private ReportArchiver _reportArchiver;
    private CsvUtils _csvUtils;

    [SetUp]
    public void Setup()
    {
        InitializeLogger();
        InitializeFileSystem();
        InitializeMonitoringController();
        InitializePaths();

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
    public async Task RanttOutput_WithOverrides_ShouldUseOverridesFromCsvFile()
    {
        // Arrange
        var csvContent = "FullName,CustomColumn\nMethodOverrideManagerAndRanttOutputTests.TestMethod(),OverrideValue";
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
            var item = CreateTestMethodLifeCycleItem("TestMethod", DateTime.Now);
            ranttOutput.WriteItem(item);
        }

        // Assert
        var csvOutputPath = Path.Combine(_testOutputPath, "TestReporter", "TestReporter.csv");
        Assert.That(_fileSystem.FileExists(csvOutputPath), Is.True, "CSV output file should be created");

        var csvOutputContent = await _fileSystem.ReadAllTextAsync(csvOutputPath);
        _logger.LogInformation($"CSV output content: {csvOutputContent}");

        Assert.That(csvOutputContent, Does.Contain("OverrideValue"), "CSV output should contain the override value");
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
            var item = CreateTestMethodLifeCycleItem("TestMethod", DateTime.Now, new Dictionary<string, string> { { "CustomColumn", "CustomValue" } });
            ranttOutput.WriteItem(item);
        }

        // Assert
        Assert.That(_fileSystem.FileExists(_overrideTemplateFilePath), Is.True, "Template file should be created");
        var lines = await _fileSystem.ReadAllLinesAsync(_overrideTemplateFilePath);

        var headerLine = lines[0];
        var headers = headerLine.Split(',');
        var fullNameIndex = Array.IndexOf(headers, "FullName");
        var customColumnIndex = Array.IndexOf(headers, "CustomColumn");

        // assert header
        Assert.That(fullNameIndex, Is.GreaterThanOrEqualTo(0), "FullName column should be present");
        Assert.That(customColumnIndex, Is.GreaterThanOrEqualTo(0), "CustomColumn column should be present");

        // assert values
        var values = lines[1].Split(',');
        Assert.That(values[fullNameIndex], Is.EqualTo($"{nameof(MethodOverrideManagerAndRanttOutputTests)}.TestMethod()"), "FullName value should be Test.Method");
        Assert.That(values[customColumnIndex], Is.EqualTo("CustomValue"), "CustomColumn value should be CustomValue");
    }

    private void InitializeFileSystem()
    {
#pragma warning disable IDISP003
        _fileSystem = new InMemoryFileSystem(_loggerFactory);
#pragma warning restore IDISP003
        _csvUtils = new CsvUtils(_fileSystem);
        _reportArchiver = new ReportArchiver(_fileSystem, _loggerFactory);
    }

    private void InitializeLogger()
    {
        _logger = new TestLogger<MethodOverrideManagerAndRanttOutputTests>();
        _loggerFactory = new TestLoggerFactory<MethodOverrideManagerAndRanttOutputTests>(_logger);

        _loggerFactory.EnableLoggingFor<RanttOutput>();
        _loggerFactory.EnableLoggingFor<ReportOutputHelper>();
        _loggerFactory.EnableLoggingFor<MethodOverrideManager>();
        _loggerFactory.EnableLoggingFor<InMemoryFileSystem>();
    }

    private void InitializeMonitoringController()
    {
        _monitoringController = new MonitoringController(_loggerFactory, () => new EnhancedDataPostProcessor(_loggerFactory));
        _methodCallInfoPool = new MethodCallInfoPool(_monitoringController, _loggerFactory);
    }

    private void InitializePaths()
    {
        _testOutputPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        _fileSystem.CreateDirectory(_testOutputPath);
        _overrideFilePath = Path.Combine(_testOutputPath, "TestReporter", "method_overrides.csv");
        _overrideTemplateFilePath = Path.Combine(_testOutputPath, "TestReporter", "method_overrides.template");
    }

    private RanttOutput CreateRanttOutput()
    {
        var ranttOutput = new RanttOutput(_loggerFactory,
            () => new EnhancedDataPostProcessor(_loggerFactory),
            new ReportOutputHelper(_loggerFactory),
            (outputFolder) => new MethodOverrideManager(outputFolder, _loggerFactory, _fileSystem, _csvUtils),
            _fileSystem,
            _reportArchiver);
        var parameters = RanttOutput.CreateParameters(_testOutputPath);
        ranttOutput.SetParameters(parameters);
        return ranttOutput;
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
}

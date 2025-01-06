#pragma warning disable CL0002
namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using Reporters.ReportOutputs;
using Reporters;
using MethodLifeCycleItems;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TestUtilities.Logging;
using TestUtilities.Mocks;
using TestUtilities.TestHelpers;
using Orc.Monitoring.TestUtilities;

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
    private RanttOutput _ranttOutput;
    private MockReporter _mockReporter;

    [SetUp]
    public void Setup()
    {
        InitializeLogger();
        InitializeFileSystem();
        InitializeMonitoringController();
        InitializePaths();

        _ranttOutput = CreateRanttOutput();
        _mockReporter = new MockReporter(_loggerFactory);

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
        var csvContent = "FullName,CustomColumn\nMethodOverrideManagerAndRanttOutputTests.TestMethod,OverrideValue";
        await _fileSystem.WriteAllTextAsync(_overrideFilePath, csvContent);

        _ranttOutput = CreateRanttOutput();
        var parameters = RanttOutput.CreateParameters(_testOutputPath);
        _ranttOutput.SetParameters(parameters);

        var mockReporter = new Mock<IMethodCallReporter>();
        mockReporter.Setup(r => r.FullName).Returns("TestReporter");
        var disposable = _ranttOutput.Initialize(mockReporter.Object);

        var methodCallInfo = CreateMethodCallInfo("TestMethod", null);
        methodCallInfo.AddParameter("CustomColumn", "OriginalValue");
        methodCallInfo.AttributeParameters.Add("CustomColumn");

        // Act
        _ranttOutput.WriteItem(new MethodCallStart(methodCallInfo));
        _ranttOutput.WriteItem(new MethodCallEnd(methodCallInfo));

        await disposable.DisposeAsync();

        // Assert
        var csvFilePath = _fileSystem.Combine(_testOutputPath, "TestReporter", "TestReporter.csv");
        Assert.That(_fileSystem.FileExists(csvFilePath), Is.True, "CSV file should exist");
        var content = await _fileSystem.ReadAllTextAsync(csvFilePath);
        var lines = await _fileSystem.ReadAllLinesAsync(csvFilePath);

        var headers = lines[0].Split(',');
        var customColumnIndex = Array.IndexOf(headers, "CustomColumn");
        
        var values = lines[1].Split(',');
        var customColumnValue = values[customColumnIndex];

        _logger.LogInformation($"CSV content:\n{content}");

        Assert.That(customColumnValue, Is.EqualTo("OverrideValue"), "Override value should be used");
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
        await using (var _ = ranttOutput.Initialize(mockReporter.Object))
        {
            var item = CreateTestMethodLifeCycleItem("TestMethod", DateTime.Now, new Dictionary<string, string> { { "CustomColumn", "CustomValue" } });
            ranttOutput.WriteItem(item);
        }

        // Assert
        Assert.That(_fileSystem.FileExists(_overrideTemplateFilePath), Is.True, $"Template file should be created at {_overrideTemplateFilePath}");

        if (_fileSystem.FileExists(_overrideTemplateFilePath))
        {
            var lines = await _fileSystem.ReadAllLinesAsync(_overrideTemplateFilePath);

            _logger.LogInformation($"Template file contents:\n{string.Join("\n", lines)}");

            Assert.That(lines.Length, Is.GreaterThanOrEqualTo(2), "Template file should have at least a header and one data line");

            var headerLine = lines[0];
            var headers = headerLine.Split(',');
            var fullNameIndex = Array.IndexOf(headers, "FullName");
            var customColumnIndex = Array.IndexOf(headers, "CustomColumn");

            Assert.That(fullNameIndex, Is.GreaterThanOrEqualTo(0), "FullName column should be present");
            Assert.That(customColumnIndex, Is.GreaterThanOrEqualTo(0), "CustomColumn column should be present");

            if (lines.Length > 1)
            {
                var values = lines[1].Split(',');
                Assert.That(values[fullNameIndex], Is.EqualTo("MethodOverrideManagerAndRanttOutputTests.TestMethod()"), "FullName value should be correct");
                Assert.That(values[customColumnIndex], Is.EqualTo("CustomValue"), "CustomColumn value should be correct");
            }
        }
    }

    private void InitializeFileSystem()
    {
#pragma warning disable IDISP003
        _fileSystem = new InMemoryFileSystem(_loggerFactory);
#pragma warning restore IDISP003
        _csvUtils = TestHelperMethods.CreateCsvUtils(_fileSystem, _loggerFactory);
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
        _monitoringController = new MonitoringController(_loggerFactory);
        _methodCallInfoPool = new MethodCallInfoPool(_monitoringController, _loggerFactory);
    }

    private void InitializePaths()
    {
        _testOutputPath = _fileSystem.Combine(_fileSystem.GetTempPath(), _fileSystem.GetRandomFileName());
        _fileSystem.CreateDirectory(_testOutputPath);
        _overrideFilePath = _fileSystem.Combine(_testOutputPath, "TestReporter", "method_overrides.csv");
        _overrideTemplateFilePath = _fileSystem.Combine(_testOutputPath, "TestReporter", "method_overrides.template");
    }

    private RanttOutput CreateRanttOutput()
    {
        var ranttOutput = new RanttOutput(_loggerFactory,
            () => new EnhancedDataPostProcessor(_loggerFactory),
            new ReportOutputHelper(_loggerFactory, new ReportItemFactory(_loggerFactory)),
            (outputFolder) => new MethodOverrideManager(outputFolder, _loggerFactory, _fileSystem, _csvUtils),
            _fileSystem,
            _reportArchiver, new ReportItemFactory(_loggerFactory), _csvUtils);
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

    private MethodCallInfo CreateMethodCallInfo(string methodName, MethodCallInfo? parent)
    {
        var methodInfo = new TestMethodInfo(methodName, typeof(MethodOverrideManagerAndRanttOutputTests));
        var id = Guid.NewGuid().ToString();
        var methodCallInfo = _methodCallInfoPool.Rent(
            null,
            typeof(MethodOverrideManagerAndRanttOutputTests),
            methodInfo,
            Array.Empty<Type>(),
            id,
            new Dictionary<string, string>()
        );
        methodCallInfo.Parent = parent;
        methodCallInfo.MethodName = methodName;
        methodCallInfo.Id = id;
        methodCallInfo.StartTime = DateTime.Now;

        // If you need to set any default parameters or attribute parameters, do it here
        // For example:
        // methodCallInfo.Parameters["SomeParam"] = "SomeValue";
        // methodCallInfo.AttributeParameters.Add("SomeAttributeParam");

        return methodCallInfo;
    }
}

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


[TestFixture]
public class CsvReportOutputTests
{
    private TestLogger<CsvReportOutputTests> _logger;
    private IMonitoringLoggerFactory _loggerFactory;
    private CsvReportOutput _csvReportOutput;
    private Mock<IMethodCallReporter> _mockReporter;
    private string _testFolderPath;
    private string _testFileName;

    [SetUp]
    public void Setup()
    {
        _logger = new TestLogger<CsvReportOutputTests>();
        _loggerFactory = new TestLoggerFactory<CsvReportOutputTests>(_logger);

        _testFolderPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_testFolderPath);
        _testFileName = "TestReport";
        var reportOutputHelper = new ReportOutputHelper(_logger.CreateLogger<ReportOutputHelper>());
        _csvReportOutput = new CsvReportOutput(_loggerFactory, reportOutputHelper, 
            (outputDirectory) => new MethodOverrideManager(outputDirectory, _logger.CreateLogger<MethodOverrideManager>()));
        _mockReporter = new Mock<IMethodCallReporter>();
        _mockReporter.Setup(r => r.FullName).Returns("TestReporter");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_testFolderPath))
        {
            Directory.Delete(_testFolderPath, true);
        }
    }

    [Test]
    public void SetParameters_SetsCorrectProperties()
    {
        var parameters = CsvReportOutput.CreateParameters(_testFolderPath, _testFileName);
        _csvReportOutput.SetParameters(parameters);

        var filePath = Path.Combine(_testFolderPath, $"{_testFileName}.csv");
        Assert.That(File.Exists(filePath), Is.False, "File should not be created yet");
    }

    [Test]
    public async Task Initialize_CreatesFileWithCorrectName()
    {
        var parameters = CsvReportOutput.CreateParameters(_testFolderPath, _testFileName);
        _csvReportOutput.SetParameters(parameters);

        var disposable = _csvReportOutput.Initialize(_mockReporter.Object);
        await disposable.DisposeAsync();

        var filePath = Path.Combine(_testFolderPath, $"{_testFileName}.csv");
        Assert.That(File.Exists(filePath), Is.True, "CSV file should be created after initialization");
    }

    [Test]
    public async Task WriteItem_AddsItemToReport()
    {
        var parameters = CsvReportOutput.CreateParameters(_testFolderPath, _testFileName);
        _csvReportOutput.SetParameters(parameters);
        var disposable = _csvReportOutput.Initialize(_mockReporter.Object);

        var methodCallInfo = MethodCallInfo.Create(new MethodCallInfoPool(_logger.CreateLogger<MethodCallInfoPool>()), null, typeof(CsvReportOutputTests),
            GetType().GetMethod(nameof(WriteItem_AddsItemToReport)),
            Array.Empty<Type>(), "TestId", new Dictionary<string, string>());

        var methodCallStart = new MethodCallStart(methodCallInfo);
        _csvReportOutput.WriteItem(methodCallStart);

        // Add an end call to ensure the data is written
        var methodCallEnd = new MethodCallEnd(methodCallInfo);
        _csvReportOutput.WriteItem(methodCallEnd);

        // Dispose to ensure data is written to file
        await disposable.DisposeAsync();

        var filePath = Path.Combine(_testFolderPath, $"{_testFileName}.csv");
        Assert.That(File.Exists(filePath), Is.True, "CSV file should be created");

        var fileContent = await File.ReadAllTextAsync(filePath);
        Console.WriteLine($"File content:\n{fileContent}"); // Log the file content for debugging

        Assert.That(fileContent, Does.Contain("TestId"), "File should contain the TestId");
        Assert.That(fileContent, Does.Contain(nameof(WriteItem_AddsItemToReport)), "File should contain the method name");
    }

    [Test]
    public void WriteSummary_DoesNotThrowException()
    {
        Assert.DoesNotThrow(() => _csvReportOutput.WriteSummary("Test summary"), "WriteSummary should not throw an exception");
    }

    [Test]
    public void WriteError_DoesNotThrowException()
    {
        Assert.DoesNotThrow(() => _csvReportOutput.WriteError(new Exception("Test exception")), "WriteError should not throw an exception");
    }
}

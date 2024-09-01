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
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Extensions.Logging;
using IO;

[TestFixture]
public class CsvReportOutputTests
{
    private TestLogger<CsvReportOutputTests> _logger;
    private IMonitoringLoggerFactory _loggerFactory;
    private CsvReportOutput _csvReportOutput;
    private Mock<IMethodCallReporter> _mockReporter;
    private string _testFolderPath;
    private string _testFileName;
    private MethodCallInfoPool _methodCallInfoPool;
    private IMonitoringController _monitoringController;

    [SetUp]
    public void Setup()
    {
        _logger = new TestLogger<CsvReportOutputTests>();
        _loggerFactory = new TestLoggerFactory<CsvReportOutputTests>(_logger);

        _monitoringController = new MonitoringController(_loggerFactory, () => new EnhancedDataPostProcessor(_loggerFactory));
        _methodCallInfoPool = new MethodCallInfoPool(_monitoringController, _loggerFactory);

        _testFolderPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_testFolderPath);
        _testFileName = "TestReport";
        var reportOutputHelper = new ReportOutputHelper(_loggerFactory);
        _csvReportOutput = new CsvReportOutput(_loggerFactory, reportOutputHelper,
            (outputDirectory) => new MethodOverrideManager(outputDirectory, _loggerFactory),
#pragma warning disable IDISP004
            new InMemoryFileSystem());
#pragma warning restore IDISP004
        _mockReporter = new Mock<IMethodCallReporter>();
        _mockReporter.Setup(r => r.FullName).Returns("TestReporter");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_testFolderPath))
        {
            try
            {
                Directory.Delete(_testFolderPath, true);
            }
            catch (UnauthorizedAccessException)
            {
                // Log the error and continue
                _logger.LogError($"Unable to delete test folder: {_testFolderPath}");
            }
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

        var methodCallInfo = _methodCallInfoPool.Rent(null, typeof(CsvReportOutputTests),
            GetType().GetMethod(nameof(WriteItem_AddsItemToReport)),
            Array.Empty<Type>(), "TestId", new Dictionary<string, string>());

        var methodCallStart = new MethodCallStart(methodCallInfo);
        _csvReportOutput.WriteItem(methodCallStart);

        var methodCallEnd = new MethodCallEnd(methodCallInfo);
        _csvReportOutput.WriteItem(methodCallEnd);

        await disposable.DisposeAsync();

        var filePath = Path.Combine(_testFolderPath, $"{_testFileName}.csv");
        Assert.That(File.Exists(filePath), Is.True, "CSV file should be created");

        var fileContent = await File.ReadAllTextAsync(filePath);
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

    [Test]
    public void Initialize_WithReadOnlyFolder_ThrowsUnauthorizedAccessException()
    {
        var readOnlyFolder = Path.Combine(_testFolderPath, "ReadOnly");
        Directory.CreateDirectory(readOnlyFolder);
        File.SetAttributes(readOnlyFolder, FileAttributes.ReadOnly);

        var parameters = CsvReportOutput.CreateParameters(readOnlyFolder, _testFileName);
        _csvReportOutput.SetParameters(parameters);

        Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
        {
            await using var _ = _csvReportOutput.Initialize(_mockReporter.Object);
        });
    }

    [Test]
    public async Task WriteItem_WithCorruptData_HandlesGracefully()
    {
        var parameters = CsvReportOutput.CreateParameters(_testFolderPath, _testFileName);
        _csvReportOutput.SetParameters(parameters);
        var disposable = _csvReportOutput.Initialize(_mockReporter.Object);

        var corruptMethodCallInfo = _methodCallInfoPool.Rent(null, typeof(CsvReportOutputTests),
            GetType().GetMethod(nameof(WriteItem_WithCorruptData_HandlesGracefully)),
            Array.Empty<Type>(), "CorruptId", new Dictionary<string, string> { { "CorruptKey", "\0InvalidValue" } });

        var methodCallStart = new MethodCallStart(corruptMethodCallInfo);
        Assert.DoesNotThrow(() => _csvReportOutput.WriteItem(methodCallStart), "WriteItem should handle corrupt data gracefully");

        await disposable.DisposeAsync();
    }

    [Test]
    public async Task Dispose_WhenFileIsLocked_HandlesGracefully()
    {
        var parameters = CsvReportOutput.CreateParameters(_testFolderPath, _testFileName);
        _csvReportOutput.SetParameters(parameters);
        var disposable = _csvReportOutput.Initialize(_mockReporter.Object);

        var filePath = Path.Combine(_testFolderPath, $"{_testFileName}.csv");

        // Lock the file
        using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            // Attempt to dispose while the file is locked
            await disposable.DisposeAsync();
        }

        // Verify that the file still exists and contains data
        Assert.That(File.Exists(filePath), Is.True, "CSV file should still exist after failed dispose");
        var fileContent = await File.ReadAllTextAsync(filePath);
        Assert.That(fileContent, Is.Not.Empty, "CSV file should contain data after failed dispose");
    }

    [Test]
    public async Task WriteItem_WhenDiskIsFull_HandlesGracefully()
    {
        var mockFileSystem = new Mock<IFileSystem>();
        mockFileSystem.Setup(fs => fs.AppendAllText(It.IsAny<string>(), It.IsAny<string>()))
            .Throws(new IOException("Disk full"));

        var csvReportOutput = new CsvReportOutput(_loggerFactory, new ReportOutputHelper(_loggerFactory),
            (outputDirectory) => new MethodOverrideManager(outputDirectory, _loggerFactory), mockFileSystem.Object);

        var parameters = CsvReportOutput.CreateParameters(_testFolderPath, _testFileName);
        csvReportOutput.SetParameters(parameters);
        var disposable = csvReportOutput.Initialize(_mockReporter.Object);

        var methodCallInfo = _methodCallInfoPool.Rent(null, typeof(CsvReportOutputTests),
            GetType().GetMethod(nameof(WriteItem_WhenDiskIsFull_HandlesGracefully)),
            Array.Empty<Type>(), "TestId", new Dictionary<string, string>());

        var methodCallStart = new MethodCallStart(methodCallInfo);
        Assert.DoesNotThrow(() => csvReportOutput.WriteItem(methodCallStart), "WriteItem should handle disk full error gracefully");

        await disposable.DisposeAsync();
    }
}

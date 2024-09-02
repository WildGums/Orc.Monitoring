#pragma warning disable CL0002
namespace Orc.Monitoring.Tests;

using Moq;
using NUnit.Framework;
using Orc.Monitoring.MethodLifeCycleItems;
using Orc.Monitoring.Reporters.ReportOutputs;
using Orc.Monitoring.Reporters;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;
using System.Linq;

[TestFixture]
public class CsvReportOutputLimitableTests
{
    private TestLogger<CsvReportOutputLimitableTests> _logger;
    private IMonitoringLoggerFactory _loggerFactory;
    private CsvReportOutput _csvReportOutput;
    private string _testOutputPath;
    private MethodCallInfoPool _methodCallInfoPool;
    private IMonitoringController _monitoringController;
    private InMemoryFileSystem _fileSystem;
    private ReportArchiver _reportArchiver;
    private CsvUtils _csvUtils;

    [SetUp]
    public void Setup()
    {
        _logger = new TestLogger<CsvReportOutputLimitableTests>();
        _loggerFactory = new TestLoggerFactory<CsvReportOutputLimitableTests>(_logger);

        _monitoringController = new MonitoringController(_loggerFactory, () => new EnhancedDataPostProcessor(_loggerFactory));
        _methodCallInfoPool = new MethodCallInfoPool(_monitoringController, _loggerFactory);
        _fileSystem = new InMemoryFileSystem();
        _csvUtils = new CsvUtils(_fileSystem);

        _reportArchiver = new ReportArchiver(_fileSystem);

        _testOutputPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        _fileSystem.CreateDirectory(_testOutputPath);

        var reportOutputHelper = new ReportOutputHelper(_loggerFactory);

        _csvReportOutput = new CsvReportOutput(_loggerFactory, reportOutputHelper,
            (outputDirectory) => new MethodOverrideManager(outputDirectory, _loggerFactory, _fileSystem, _csvUtils),
            _fileSystem, _reportArchiver);
        var parameters = CsvReportOutput.CreateParameters(_testOutputPath, "TestReport");
        _csvReportOutput.SetParameters(parameters);
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
        var options = OutputLimitOptions.LimitItems(100);
        _csvReportOutput.SetLimitOptions(options);
        var retrievedOptions = _csvReportOutput.GetLimitOptions();

        Assert.That(retrievedOptions.MaxItems, Is.EqualTo(options.MaxItems));
    }

    [Test]
    public void GetLimitOptions_ReturnsDefaultOptionsInitially()
    {
        var options = _csvReportOutput.GetLimitOptions();
        Assert.That(options.MaxItems, Is.Null);
    }

    [Test]
    public async Task WriteItem_RespectsItemCountLimit()
    {
        _csvReportOutput.SetLimitOptions(OutputLimitOptions.LimitItems(5));
        var mockReporter = new Mock<IMethodCallReporter>();
        await using (var disposable = _csvReportOutput.Initialize(mockReporter.Object))
        {
            for (int i = 0; i < 10; i++)
            {
                var item = CreateTestMethodLifeCycleItem($"Item{i}", DateTime.Now.AddMinutes(-i));
                _csvReportOutput.WriteItem(item);
            }
        }

        var filePath = Path.Combine(_testOutputPath, "TestReport.csv");
        var lines = (await _fileSystem.ReadAllTextAsync(filePath))
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        _logger.LogInformation($"CSV file contents ({lines.Length} lines):");
        foreach (var line in lines)
        {
            _logger.LogInformation(line);
        }

        Assert.That(lines.Length, Is.EqualTo(6), $"Expected 6 lines (header + 5 items), but got {lines.Length} lines");
        Assert.That(lines[0], Does.Contain("Id"), "First line should be the header");
        Assert.That(lines.Skip(1).Count(), Is.EqualTo(5), "Should have 5 data lines");
    }

    [Test]
    public async Task WriteItem_WithNoLimit_WritesAllItems()
    {
        var mockReporter = new Mock<IMethodCallReporter>();
        await using (var disposable = _csvReportOutput.Initialize(mockReporter.Object))
        {
            for (int i = 0; i < 10; i++)
            {
                var item = CreateTestMethodLifeCycleItem($"Item{i}", DateTime.Now.AddMinutes(-i));
                _csvReportOutput.WriteItem(item);
            }
        }

        var filePath = Path.Combine(_testOutputPath, "TestReport.csv");
        var lines = (await _fileSystem.ReadAllTextAsync(filePath))
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        Assert.That(lines.Length, Is.EqualTo(11)); // Header + 10 items
    }

    private ICallStackItem CreateTestMethodLifeCycleItem(string itemName, DateTime timestamp)
    {
        var methodInfo = new TestMethodInfo(itemName, typeof(CsvReportOutputLimitableTests));
        var methodCallInfo = _methodCallInfoPool.Rent(
            null,
            typeof(CsvReportOutputLimitableTests),
            methodInfo,
            Array.Empty<Type>(),
            Guid.NewGuid().ToString(),
            new Dictionary<string, string>()
        );
        methodCallInfo.StartTime = timestamp;

        var mockMethodLifeCycleItem = new Mock<MethodCallStart>(methodCallInfo);
        return mockMethodLifeCycleItem.Object;
    }
}

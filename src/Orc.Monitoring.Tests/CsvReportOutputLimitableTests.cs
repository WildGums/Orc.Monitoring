#pragma warning disable CL0002
namespace Orc.Monitoring.Tests;

using Moq;
using NUnit.Framework;
using Reporters.ReportOutputs;
using Reporters;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Core.Abstractions;
using Core.Controllers;
using Core.Factories;
using Core.MethodLifecycle;
using Core.Pooling;
using TestUtilities;
using TestUtilities.Logging;
using TestUtilities.Mocks;
using TestUtilities.TestHelpers;
using Utilities.Logging;
using Utilities.Serialization;

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

        _monitoringController = new MonitoringController(_loggerFactory);
        _methodCallInfoPool = new MethodCallInfoPool(_monitoringController, _loggerFactory);
        _fileSystem = new InMemoryFileSystem(_loggerFactory);
        _csvUtils = TestHelperMethods.CreateCsvUtils(_fileSystem, _loggerFactory);

        _reportArchiver = new ReportArchiver(_fileSystem, _loggerFactory);

        _testOutputPath = _fileSystem.Combine(_fileSystem.GetTempPath(), _fileSystem.GetRandomFileName());

        _fileSystem.CreateDirectory(_testOutputPath);

        var reportOutputHelper = new ReportOutputHelper(_loggerFactory, new ReportItemFactory(_loggerFactory));

        _csvReportOutput = new CsvReportOutput(_loggerFactory, reportOutputHelper,
            (outputDirectory) => new MethodOverrideManager(outputDirectory, _loggerFactory, _fileSystem, _csvUtils),
            _fileSystem, _reportArchiver);
        var parameters = CsvReportOutput.CreateParameters(_testOutputPath, TestConstants.DefaultReportName);
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
        var options = OutputLimitOptions.LimitItems(TestConstants.DefaultTestMaxItems);
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
        _csvReportOutput.SetLimitOptions(OutputLimitOptions.LimitItems(TestConstants.DefaultTestMaxItems));
        var mockReporter = new Mock<IMethodCallReporter>();
        mockReporter.Setup(r => r.FullName).Returns(TestConstants.DefaultTestReporterName);
        await using (var _ = _csvReportOutput.Initialize(mockReporter.Object))
        {
            for (int i = 0; i < TestConstants.DefaultTestMaxItems * 2; i++)
            {
                var item = CreateTestMethodLifeCycleItem($"{TestConstants.DefaultTestMethodName}{i}", TestConstants.DefaultItemStartTime.AddMinutes(-i));
                _csvReportOutput.WriteItem(item);
            }
        }

        var filePath = _fileSystem.Combine(_testOutputPath, TestConstants.DefaultCsvReportFileName);
        var lines = (await _fileSystem.ReadAllTextAsync(filePath))
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);

        Assert.That(lines.Length, Is.EqualTo(TestConstants.DefaultTestMaxItems + 1), $"Expected {TestConstants.DefaultTestMaxItems + 1} lines (header + {TestConstants.DefaultTestMaxItems} items)");
        Assert.That(lines[0], Does.Contain(TestConstants.CsvHeaderLine), "First line should be the header");
        Assert.That(lines.Skip(1).Count(), Is.EqualTo(TestConstants.DefaultTestMaxItems), $"Should have {TestConstants.DefaultTestMaxItems} data lines");
    }

    [Test]
    public async Task WriteItem_WithNoLimit_WritesAllItems()
    {
        var mockReporter = new Mock<IMethodCallReporter>();
        mockReporter.Setup(r => r.FullName).Returns(TestConstants.DefaultTestReporterName);
        await using (var _ = _csvReportOutput.Initialize(mockReporter.Object))
        {
            for (int i = 0; i < TestConstants.DefaultItemCount; i++)
            {
                var item = CreateTestMethodLifeCycleItem($"{TestConstants.DefaultTestMethodName}{i}", TestConstants.DefaultItemStartTime.AddMinutes(-i));
                _csvReportOutput.WriteItem(item);
            }
        }

        var filePath = _fileSystem.Combine(_testOutputPath, TestConstants.DefaultCsvReportFileName);
        var lines = (await _fileSystem.ReadAllTextAsync(filePath))
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
        Assert.That(lines.Length, Is.EqualTo(TestConstants.DefaultItemCount + 1), $"Expected {TestConstants.DefaultItemCount + 1} lines (header + {TestConstants.DefaultItemCount} items)");
    }

    private ICallStackItem CreateTestMethodLifeCycleItem(string itemName, DateTime timestamp)
    {
        var methodInfo = new TestMethodInfo(itemName, typeof(CsvReportOutputLimitableTests));
        var methodCallInfo = _methodCallInfoPool.Rent(
            null,
            typeof(CsvReportOutputLimitableTests),
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

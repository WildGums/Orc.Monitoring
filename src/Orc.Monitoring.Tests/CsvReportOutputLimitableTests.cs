#pragma warning disable CL0002
namespace Orc.Monitoring.Tests;

using Moq;
using NUnit.Framework;
using Orc.Monitoring.MethodLifeCycleItems;
using Orc.Monitoring.Reporters.ReportOutputs;
using Orc.Monitoring.Reporters;
using System.IO;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;

[TestFixture]
public class CsvReportOutputLimitableTests
{
    private TestLogger<CsvReportOutputLimitableTests> _logger;
    private CsvReportOutput _csvReportOutput;
    private string _testOutputPath;

    [SetUp]
    public void Setup()
    {
        _logger = new TestLogger<CsvReportOutputLimitableTests>();
        _testOutputPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        Directory.CreateDirectory(_testOutputPath);

        var reportOutputHelper = new ReportOutputHelper(_logger.CreateLogger<ReportOutputHelper>());

        _csvReportOutput = new CsvReportOutput(_logger.CreateLogger<CsvReportOutput>(), reportOutputHelper);
        var parameters = CsvReportOutput.CreateParameters(_testOutputPath, "TestReport");
        _csvReportOutput.SetParameters(parameters);
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
        var lines = await File.ReadAllLinesAsync(filePath);
        Assert.That(lines.Length, Is.EqualTo(6)); // Header + 5 items
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
        var lines = await File.ReadAllLinesAsync(filePath);
        Assert.That(lines.Length, Is.EqualTo(11)); // Header + 10 items
    }

    private ICallStackItem CreateTestMethodLifeCycleItem(string itemName, DateTime timestamp)
    {
        var methodInfo = new TestMethodInfo(itemName, typeof(CsvReportOutputLimitableTests));
        var methodCallInfo = MethodCallInfo.Create(
            new MethodCallInfoPool(),
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

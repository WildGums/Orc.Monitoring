#pragma warning disable CL0002
namespace Orc.Monitoring.Tests;

using Moq;
using NUnit.Framework;
using Orc.Monitoring.MethodLifeCycleItems;
using Orc.Monitoring.Reporters.ReportOutputs;
using Orc.Monitoring.Reporters;
using System.Reflection;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.IO;

[TestFixture]
public class TxtReportOutputLimitableTests
{
    private TxtReportOutput _txtReportOutput;
    private string _testOutputPath;
    private TestLogger<TxtReportOutputLimitableTests> _logger;
    private TestLoggerFactory<TxtReportOutputLimitableTests> _loggerFactory;
    private IMonitoringController _monitoringController;
    private MethodCallInfoPool _methodCallInfoPool;
    private InMemoryFileSystem _fileSystem;
    private ReportArchiver _reportArchiver;

    [SetUp]
    public void Setup()
    {
        _logger = new TestLogger<TxtReportOutputLimitableTests>();
        _loggerFactory = new TestLoggerFactory<TxtReportOutputLimitableTests>(_logger);
        _fileSystem = new InMemoryFileSystem(_loggerFactory);
        _reportArchiver = new ReportArchiver(_fileSystem, _loggerFactory);
        _monitoringController = new MonitoringController(_loggerFactory, () => new EnhancedDataPostProcessor(_loggerFactory));
        _methodCallInfoPool = new MethodCallInfoPool(_monitoringController, _loggerFactory);

        _testOutputPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        _fileSystem.CreateDirectory(_testOutputPath);
        
        var reportOutputHelper = new ReportOutputHelper(_loggerFactory);
        _txtReportOutput = new TxtReportOutput(_loggerFactory, reportOutputHelper, _reportArchiver, _fileSystem);

        var parameters = TxtReportOutput.CreateParameters(_testOutputPath, "TestDisplay");
        _txtReportOutput.SetParameters(parameters);

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
    public void SetLimitOptions_SetsOptionsCorrectly()
    {
        var options = OutputLimitOptions.LimitItems(100);
        _txtReportOutput.SetLimitOptions(options);
        var retrievedOptions = _txtReportOutput.GetLimitOptions();

        Assert.That(retrievedOptions.MaxItems, Is.EqualTo(options.MaxItems));
    }

    [Test]
    public void GetLimitOptions_ReturnsDefaultOptionsInitially()
    {
        var options = _txtReportOutput.GetLimitOptions();
        Assert.That(options.MaxItems, Is.Null);
    }

    [Test]
    public async Task WriteItem_RespectsItemCountLimit()
    {
        _txtReportOutput.SetLimitOptions(OutputLimitOptions.LimitItems(5));
        var mockReporter = new Mock<IMethodCallReporter>();
        mockReporter.Setup(r => r.Name).Returns("TestReporter");
        mockReporter.Setup(r => r.RootMethod).Returns((MethodInfo)null);
        await using (var disposable = _txtReportOutput.Initialize(mockReporter.Object))
        {
            for (int i = 0; i < 10; i++)
            {
                var item = CreateTestMethodLifeCycleItem($"Item{i}", DateTime.Now.AddMinutes(-i));
                _txtReportOutput.WriteItem(item);
            }
        }

        var filePath = Path.Combine(_testOutputPath, "TestReporter_TestDisplay.txt");
        var lines = await _fileSystem.ReadAllLinesAsync(filePath);
        Assert.That(lines.Length, Is.EqualTo(5), "Should have 5 items");
    }

    [Test]
    public async Task WriteItem_WithNoLimit_WritesAllItems()
    {
        var mockReporter = new Mock<IMethodCallReporter>();
        mockReporter.Setup(r => r.Name).Returns("TestReporter");
        mockReporter.Setup(r => r.RootMethod).Returns((MethodInfo)null);
        await using (var disposable = _txtReportOutput.Initialize(mockReporter.Object))
        {
            for (int i = 0; i < 10; i++)
            {
                var item = CreateTestMethodLifeCycleItem($"Item{i}", DateTime.Now.AddMinutes(-i));
                _txtReportOutput.WriteItem(item);
            }
        }

        var filePath = Path.Combine(_testOutputPath, "TestReporter_TestDisplay.txt");
        var lines = await _fileSystem.ReadAllLinesAsync(filePath);
        Assert.That(lines.Length, Is.EqualTo(10), "Should have all 10 items");
    }

    private ICallStackItem CreateTestMethodLifeCycleItem(string itemName, DateTime timestamp)
    {
        var methodInfo = new TestMethodInfo(itemName, typeof(TxtReportOutputLimitableTests));
        var methodCallInfo = _methodCallInfoPool.Rent(
            null,
            typeof(TxtReportOutputLimitableTests),
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

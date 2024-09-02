#pragma warning disable CL0002
namespace Orc.Monitoring.Tests;

using Moq;
using NUnit.Framework;
using MethodLifeCycleItems;
using Reporters.ReportOutputs;
using Reporters;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

[TestFixture]
public class RanttOutputLimitableTests
{
    private TestLogger<RanttOutputLimitableTests> _logger;
    private TestLoggerFactory<RanttOutputLimitableTests> _loggerFactory;
    private MethodCallInfoPool _methodCallInfoPool;
    private IMonitoringController _monitoringController;
    private RanttOutput _ranttOutput;
    private string _testOutputPath;
    private InMemoryFileSystem _fileSystem;
    private ReportArchiver _reportArchiver;
    private CsvUtils _csvUtils;

    [SetUp]
    public void Setup()
    {
        _logger = new TestLogger<RanttOutputLimitableTests>();
        _loggerFactory = new TestLoggerFactory<RanttOutputLimitableTests>(_logger);
        _fileSystem = new InMemoryFileSystem();
        _csvUtils = new CsvUtils(_fileSystem);
        _reportArchiver = new ReportArchiver(_fileSystem);
        _monitoringController = new MonitoringController(_loggerFactory, () => new EnhancedDataPostProcessor(_loggerFactory));
        _methodCallInfoPool = new MethodCallInfoPool(_monitoringController, _loggerFactory);

        _testOutputPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        _fileSystem.CreateDirectory(_testOutputPath);
        _ranttOutput = new RanttOutput(_loggerFactory, 
            () => new EnhancedDataPostProcessor(_loggerFactory),
            new ReportOutputHelper(_loggerFactory),
            (outputDirectory) => new MethodOverrideManager(outputDirectory, _loggerFactory, _fileSystem, _csvUtils),
            _fileSystem,
            _reportArchiver);
        var parameters = RanttOutput.CreateParameters(_testOutputPath);
        _ranttOutput.SetParameters(parameters);

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
        _ranttOutput.SetLimitOptions(options);
        var retrievedOptions = _ranttOutput.GetLimitOptions();

        Assert.That(retrievedOptions.MaxItems, Is.EqualTo(options.MaxItems));
    }

    [Test]
    public void GetLimitOptions_ReturnsDefaultOptionsInitially()
    {
        var options = _ranttOutput.GetLimitOptions();
        Assert.That(options.MaxItems, Is.Null);
    }

    [Test]
    public async Task WriteItem_RespectsItemCountLimit()
    {
        _ranttOutput.SetLimitOptions(OutputLimitOptions.LimitItems(5));
        var mockReporter = new Mock<IMethodCallReporter>();
        mockReporter.Setup(r => r.FullName).Returns("TestReporter");
        await using (var disposable = _ranttOutput.Initialize(mockReporter.Object))
        {
            for (int i = 0; i < 10; i++)
            {
                var item = CreateTestMethodLifeCycleItem($"Item{i}", DateTime.Now.AddMinutes(-i));
                _ranttOutput.WriteItem(item);
            }
        }

        var filePath = Path.Combine(_testOutputPath, "TestReporter", "TestReporter.csv");
        var lines = await _fileSystem.ReadAllLinesAsync(filePath);

        Console.WriteLine($"File content:\n{string.Join("\n", lines)}");

        Assert.That(lines.Length, Is.EqualTo(6), "Expected 6 lines (header + 5 items)");
        Assert.That(lines[0], Does.Contain("Id"), "First line should be the header");
        Assert.That(lines.Skip(1).Count(), Is.EqualTo(5), "Should have 5 items");
    }

    [Test]
    public async Task WriteItem_WithNoLimit_WritesAllItems()
    {
        var mockReporter = new Mock<IMethodCallReporter>();
        mockReporter.Setup(r => r.FullName).Returns("TestReporter");
        await using (var disposable = _ranttOutput.Initialize(mockReporter.Object))
        {
            for (int i = 0; i < 10; i++)
            {
                var item = CreateTestMethodLifeCycleItem($"Item{i}", DateTime.Now.AddMinutes(-i));
                _ranttOutput.WriteItem(item);
            }
        }

        var filePath = Path.Combine(_testOutputPath, "TestReporter", "TestReporter.csv");
        var lines = await _fileSystem.ReadAllLinesAsync(filePath);

        Console.WriteLine($"File content:\n{string.Join("\n", lines)}");

        Assert.That(lines.Length, Is.EqualTo(11), "Expected 11 lines (header + 10 items)");
        Assert.That(lines[0], Does.Contain("Id"), "First line should be the header");
        Assert.That(lines.Skip(1).Count(), Is.EqualTo(10), "Should have 10 items");
    }

    private ICallStackItem CreateTestMethodLifeCycleItem(string itemName, DateTime timestamp)
    {
        var methodInfo = new TestMethodInfo(itemName, typeof(RanttOutputLimitableTests));
        var methodCallInfo = _methodCallInfoPool.Rent(
            null,
            typeof(RanttOutputLimitableTests),
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

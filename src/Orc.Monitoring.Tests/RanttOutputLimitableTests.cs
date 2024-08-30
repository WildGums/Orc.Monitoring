#pragma warning disable CL0002
namespace Orc.Monitoring.Tests;

using Moq;
using NUnit.Framework;
using MethodLifeCycleItems;
using Reporters.ReportOutputs;
using Reporters;
using System.IO;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;

[TestFixture]
public class RanttOutputLimitableTests
{
    private TestLogger<RanttOutputLimitableTests> _logger;
    private RanttOutput _ranttOutput;
    private string _testOutputPath;

    [SetUp]
    public void Setup()
    {
        _logger = new TestLogger<RanttOutputLimitableTests>();
        _testOutputPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_testOutputPath);
        _ranttOutput = new RanttOutput(_logger.CreateLogger<RanttOutput>(), 
            () => new EnhancedDataPostProcessor(_logger.CreateLogger<EnhancedDataPostProcessor>()),
            new ReportOutputHelper(_logger.CreateLogger<ReportOutputHelper>()),
            (outputDirectory) => new MethodOverrideManager(outputDirectory, _logger.CreateLogger<MethodOverrideManager>()));
        var parameters = RanttOutput.CreateParameters(_testOutputPath);
        _ranttOutput.SetParameters(parameters);
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
        var lines = await File.ReadAllLinesAsync(filePath);

        Console.WriteLine($"File content:\n{string.Join("\n", lines)}");

        Assert.That(lines.Length, Is.EqualTo(7), $"Expected 7 lines (header + ROOT + 5 items), but got {lines.Length}");
        Assert.That(lines[0], Does.Contain("Id"), "First line should be the header");
        Assert.That(lines[1], Does.Contain("ROOT"), "Second line should contain ROOT node");
        Assert.That(lines.Skip(2).Count(), Is.EqualTo(5), "Should have 5 non-ROOT items");
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
        var lines = await File.ReadAllLinesAsync(filePath);

        // Log the content of the file
        Console.WriteLine($"File content:\n{string.Join("\n", lines)}");

        Assert.That(lines.Length, Is.EqualTo(12), $"Expected 12 lines (header + ROOT + 10 items), but got {lines.Length}");
        Assert.That(lines[0], Does.Contain("Id"), "First line should be the header");
        Assert.That(lines[1], Does.Contain("ROOT"), "Second line should contain ROOT node");
        Assert.That(lines.Skip(2).Count(), Is.EqualTo(10), "Should have 10 non-ROOT items");
    }

    private ICallStackItem CreateTestMethodLifeCycleItem(string itemName, DateTime timestamp)
    {
        var methodInfo = new TestMethodInfo(itemName, typeof(RanttOutputLimitableTests));
        var methodCallInfo = MethodCallInfo.Create(
            new MethodCallInfoPool(_logger.CreateLogger<MethodCallInfoPool>()),
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

#pragma warning disable CL0002
namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using Orc.Monitoring.Reporters;
using Orc.Monitoring.Reporters.ReportOutputs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orc.Monitoring.MethodLifeCycleItems;
using Moq;

[TestFixture]
public class RanttOutputTests
{
    private RanttOutput _ranttOutput;
    private MockReporter _mockReporter;
    private string _testFolderPath;
    private TestLogger<RanttOutputTests> _logger;
    private Mock<IEnhancedDataPostProcessor> _mockPostProcessor;

    [SetUp]
    public void Setup()
    {
        _logger = new TestLogger<RanttOutputTests>();
        _testFolderPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_testFolderPath);
        _mockReporter = new MockReporter(_logger.CreateLogger<MockReporter>()) { Name = "TestReporter", FullName = "TestReporter" };
        _mockPostProcessor = new Mock<IEnhancedDataPostProcessor>();
        _ranttOutput = new RanttOutput(_logger.CreateLogger<RanttOutput>(), 
            () => _mockPostProcessor.Object,
            new ReportOutputHelper(_logger.CreateLogger<ReportOutputHelper>()),
            (outputFolder) => new MethodOverrideManager(outputFolder, _logger.CreateLogger<MethodOverrideManager>()));
        var parameters = RanttOutput.CreateParameters(_testFolderPath);
        _ranttOutput.SetParameters(parameters);
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
    public async Task WriteItem_CorrectlyGeneratesRelationships()
    {
        // Arrange
        var disposable = _ranttOutput.Initialize(_mockReporter);

        var parentMethodInfo = CreateMethodCallInfo("ParentMethod", null);
        var parentStart = new MethodCallStart(parentMethodInfo);
        _ranttOutput.WriteItem(parentStart);

        var childMethodInfo = CreateMethodCallInfo("ChildMethod", parentMethodInfo);
        var childStart = new MethodCallStart(childMethodInfo);
        _ranttOutput.WriteItem(childStart);

        var childEnd = new MethodCallEnd(childMethodInfo);
        _ranttOutput.WriteItem(childEnd);
        var parentEnd = new MethodCallEnd(parentMethodInfo);
        _ranttOutput.WriteItem(parentEnd);

        // Mock the post-processor to return the same items
        _mockPostProcessor.Setup(p => p.PostProcessData(It.IsAny<List<ReportItem>>(), It.IsAny<OrphanedNodeStrategy>()))
            .Returns((List<ReportItem> items, OrphanedNodeStrategy strategy) => items);

        // Act
        await disposable.DisposeAsync();

        // Assert
        var relationshipsFilePath = Path.Combine(_testFolderPath, "TestReporter", "TestReporter_Relationships.csv");
        Assert.That(File.Exists(relationshipsFilePath), Is.True, "Relationships file should exist");

        var relationshipsContent = await File.ReadAllTextAsync(relationshipsFilePath);
        _logger.LogInformation($"Relationships file content:\n{relationshipsContent}");

        var lines = relationshipsContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        _logger.LogInformation($"Number of lines: {lines.Length}");
        for (int i = 0; i < lines.Length; i++)
        {
            _logger.LogInformation($"Line {i}: {lines[i]}");
        }

        Assert.That(lines.Length, Is.GreaterThan(1), "Relationships file should have more than just the header");

        _logger.LogInformation($"ParentMethodInfo Id: {parentMethodInfo.Id}");
        _logger.LogInformation($"ChildMethodInfo Id: {childMethodInfo.Id}");

        Assert.That(lines.Any(l => l.StartsWith($"{parentMethodInfo.Id},{childMethodInfo.Id}")), Is.True,
            $"Relationship between parent and child should be present. Expected: {parentMethodInfo.Id},{childMethodInfo.Id}");

        _mockPostProcessor.Verify(p => p.PostProcessData(It.IsAny<List<ReportItem>>(), It.IsAny<OrphanedNodeStrategy>()), Times.Exactly(2));
    }

    [Test]
    public async Task ExportToCsv_AppliesPostProcessing()
    {
        // Arrange
        var disposable = _ranttOutput.Initialize(_mockReporter);

        var methodInfo1 = CreateMethodCallInfo("Method1", null);
        var methodInfo2 = CreateMethodCallInfo("Method2", methodInfo1);

        _ranttOutput.WriteItem(new MethodCallStart(methodInfo1));
        _ranttOutput.WriteItem(new MethodCallStart(methodInfo2));
        _ranttOutput.WriteItem(new MethodCallEnd(methodInfo2));
        _ranttOutput.WriteItem(new MethodCallEnd(methodInfo1));

        // Mock the post-processor to return modified items, including the ROOT node
        _mockPostProcessor.Setup(p => p.PostProcessData(It.IsAny<List<ReportItem>>(), It.IsAny<OrphanedNodeStrategy>()))
            .Returns((List<ReportItem> items, OrphanedNodeStrategy strategy) =>
            {
                var rootItem = new ReportItem
                {
                    Id = "ROOT",
                    MethodName = "Root",
                    Parent = null,
                    StartTime = items.Min(r => r.StartTime),
                    EndTime = items.Max(r => r.EndTime)
                };
                items.Insert(0, rootItem); // Add ROOT as the first item
                items.First(i => i.Id == methodInfo1.Id).Parent = "ROOT";
                return items;
            });

        // Act
        await disposable.DisposeAsync();

        // Assert
        var csvFilePath = Path.Combine(_testFolderPath, "TestReporter", "TestReporter.csv");
        Assert.That(File.Exists(csvFilePath), Is.True, "CSV file should exist");

        var csvContent = await File.ReadAllTextAsync(csvFilePath);
        _logger.LogInformation($"CSV file content:\n{csvContent}");

        var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.That(lines[1], Does.StartWith("ROOT,"), "ROOT node should have an empty parent");
        Assert.That(lines[2], Does.StartWith($"{methodInfo1.Id},ROOT"), "Method1 should be a child of ROOT");
        Assert.That(lines[3], Does.StartWith($"{methodInfo2.Id},{methodInfo1.Id}"), "Method2 should be a child of Method1");

        _mockPostProcessor.Verify(p => p.PostProcessData(It.IsAny<List<ReportItem>>(), It.IsAny<OrphanedNodeStrategy>()), Times.Exactly(2));
    }

    private MethodCallInfo CreateMethodCallInfo(string methodName, MethodCallInfo? parent)
    {
        var methodInfo = new TestMethodInfo(methodName, typeof(RanttOutputTests));
        var methodCallInfo = MethodCallInfo.Create(
            new MethodCallInfoPool(_logger.CreateLogger<MethodCallInfoPool>()),
            null,
            typeof(RanttOutputTests),
            methodInfo,
            Array.Empty<Type>(),
            Guid.NewGuid().ToString(),
            new Dictionary<string, string>()
        );
        methodCallInfo.Parent = parent;
        return methodCallInfo;
    }
}

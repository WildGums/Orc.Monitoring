#pragma warning disable CL0002
namespace Orc.Monitoring.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using Orc.Monitoring.MethodLifeCycleItems;
using Orc.Monitoring.Reporters;
using Orc.Monitoring.Reporters.ReportOutputs;

[TestFixture]
public class RanttOutputPostProcessingTests
{
    private RanttOutput _ranttOutput;
    private Mock<ILogger<RanttOutput>> _loggerMock;
    private Mock<IMethodCallReporter> _reporterMock;
    private Mock<EnhancedDataPostProcessor> _postProcessorMock;
    private string _testOutputPath;

    [SetUp]
    public void Setup()
    {
        _testOutputPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_testOutputPath);

        _loggerMock = new Mock<ILogger<RanttOutput>>();
        _reporterMock = new Mock<IMethodCallReporter>();
        _postProcessorMock = new Mock<EnhancedDataPostProcessor>(MockBehavior.Strict, new Mock<ILogger<EnhancedDataPostProcessor>>().Object);

        _ranttOutput = new RanttOutput(_loggerMock.Object, () => _postProcessorMock.Object);
        var parameters = RanttOutput.CreateParameters(_testOutputPath, orphanedNodeStrategy: EnhancedDataPostProcessor.OrphanedNodeStrategy.AttachToNearestAncestor);
        _ranttOutput.SetParameters(parameters);

        _reporterMock.Setup(r => r.FullName).Returns("TestReporter");
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
    public async Task ExportToCsv_WithOrphanedNodes_AppliesPostProcessing()
    {
        // Arrange
        var reportItems = new List<ReportItem>
        {
            CreateReportItem("1", "ROOT", "Method1"),
            CreateReportItem("2", "1", "Method2"),
            CreateReportItem("3", "999", "OrphanedMethod") // Orphaned node
        };

        var processedItems = new List<ReportItem>
        {
            CreateReportItem("1", "ROOT", "Method1"),
            CreateReportItem("2", "1", "Method2"),
            CreateReportItem("3", "1", "OrphanedMethod") // Attached to nearest ancestor
        };

        _postProcessorMock.Setup(p => p.PostProcessData(It.IsAny<List<ReportItem>>(), EnhancedDataPostProcessor.OrphanedNodeStrategy.AttachToNearestAncestor))
            .Returns(processedItems);

        // Act
        await InitializeAndExportData(reportItems);

        // Assert
        var csvFilePath = Path.Combine(_testOutputPath, "TestReporter", "TestReporter.csv");
        Assert.That(File.Exists(csvFilePath), Is.True);

        var csvContent = await File.ReadAllTextAsync(csvFilePath);
        Assert.That(csvContent, Does.Contain("OrphanedMethod"));
        Assert.That(csvContent, Does.Contain("1,3,"));

        _postProcessorMock.Verify(p => p.PostProcessData(It.IsAny<List<ReportItem>>(), EnhancedDataPostProcessor.OrphanedNodeStrategy.AttachToNearestAncestor), Times.Once);
    }

    [Test]
    public async Task ExportRelationships_EnsuresRootNodeExists()
    {
        // Arrange
        var reportItems = new List<ReportItem>
        {
            CreateReportItem("1", null, "Method1"),
            CreateReportItem("2", "1", "Method2")
        };

        var processedItems = new List<ReportItem>
        {
            CreateReportItem("ROOT", null, "Root"),
            CreateReportItem("1", "ROOT", "Method1"),
            CreateReportItem("2", "1", "Method2")
        };

        _postProcessorMock.Setup(p => p.PostProcessData(It.IsAny<List<ReportItem>>(), EnhancedDataPostProcessor.OrphanedNodeStrategy.AttachToNearestAncestor))
            .Returns(processedItems);

        // Act
        await InitializeAndExportData(reportItems);

        // Assert
        var relationshipsFilePath = Path.Combine(_testOutputPath, "TestReporter", "TestReporter_Relationships.csv");
        Assert.That(File.Exists(relationshipsFilePath), Is.True);

        var relationshipsContent = await File.ReadAllTextAsync(relationshipsFilePath);
        Assert.That(relationshipsContent, Does.Contain("ROOT,1,"));
        Assert.That(relationshipsContent, Does.Contain("1,2,"));
    }

    [Test]
    public async Task ExportToCsv_WithDifferentOrphanedNodeStrategies_AppliesCorrectStrategy()
    {
        // Arrange
        var reportItems = new List<ReportItem>
        {
            CreateReportItem("1", "ROOT", "Method1"),
            CreateReportItem("2", "999", "OrphanedMethod")
        };

        _postProcessorMock.Setup(p => p.PostProcessData(It.IsAny<List<ReportItem>>(), It.IsAny<EnhancedDataPostProcessor.OrphanedNodeStrategy>()))
            .Returns((List<ReportItem> items, EnhancedDataPostProcessor.OrphanedNodeStrategy strategy) =>
            {
                if (strategy == EnhancedDataPostProcessor.OrphanedNodeStrategy.RemoveOrphans)
                {
                    return items.Where(i => i.Parent == "ROOT").ToList();
                }
                return items;
            });

        // Act & Assert
        // Test RemoveOrphans strategy
        var removeOrphansParameters = RanttOutput.CreateParameters(_testOutputPath, orphanedNodeStrategy: EnhancedDataPostProcessor.OrphanedNodeStrategy.RemoveOrphans);
        _ranttOutput.SetParameters(removeOrphansParameters);

        await InitializeAndExportData(reportItems);

        var csvFilePath = Path.Combine(_testOutputPath, "TestReporter", "TestReporter.csv");
        var csvContent = await File.ReadAllTextAsync(csvFilePath);
        Assert.That(csvContent, Does.Contain("Method1"));
        Assert.That(csvContent, Does.Not.Contain("OrphanedMethod"));

        // Test AttachToRoot strategy
        var attachToRootParameters = RanttOutput.CreateParameters(_testOutputPath, orphanedNodeStrategy: EnhancedDataPostProcessor.OrphanedNodeStrategy.AttachToRoot);
        _ranttOutput.SetParameters(attachToRootParameters);

        await InitializeAndExportData(reportItems);

        csvContent = await File.ReadAllTextAsync(csvFilePath);
        Assert.That(csvContent, Does.Contain("Method1"));
        Assert.That(csvContent, Does.Contain("OrphanedMethod"));

        _postProcessorMock.Verify(p => p.PostProcessData(It.IsAny<List<ReportItem>>(), EnhancedDataPostProcessor.OrphanedNodeStrategy.RemoveOrphans), Times.Once);
        _postProcessorMock.Verify(p => p.PostProcessData(It.IsAny<List<ReportItem>>(), EnhancedDataPostProcessor.OrphanedNodeStrategy.AttachToRoot), Times.Once);
    }

    private async Task InitializeAndExportData(List<ReportItem> reportItems)
    {
        var helper = new ReportOutputHelper();
        helper.Initialize(_reporterMock.Object);
        foreach (var item in reportItems)
        {
            helper.ProcessCallStackItem(CreateMethodCallStart(item));
        }

        var disposable = _ranttOutput.Initialize(_reporterMock.Object);
        foreach (var item in reportItems)
        {
            _ranttOutput.WriteItem(CreateMethodCallStart(item));
        }
        await disposable.DisposeAsync();
    }

    private ReportItem CreateReportItem(string id, string parent, string methodName)
    {
        return new ReportItem
        {
            Id = id,
            Parent = parent,
            MethodName = methodName,
            StartTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
            EndTime = DateTime.Now.AddSeconds(1).ToString("yyyy-MM-dd HH:mm:ss.fff")
        };
    }

    private MethodCallStart CreateMethodCallStart(ReportItem item)
    {
        var methodInfo = new TestMethodInfo(item.MethodName, typeof(RanttOutputPostProcessingTests));
        var methodCallInfo = MethodCallInfo.Create(
            new MethodCallInfoPool(),
            null,
            typeof(RanttOutputPostProcessingTests),
            methodInfo,
            Array.Empty<Type>(),
            item.Id,
            new Dictionary<string, string>()
        );
        methodCallInfo.Parent = item.Parent == "ROOT" ? MethodCallInfo.CreateNull() : null;
        return new MethodCallStart(methodCallInfo);
    }
}

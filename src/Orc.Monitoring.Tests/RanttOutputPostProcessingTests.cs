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
#pragma warning disable CL0002

[TestFixture]
public class RanttOutputPostProcessingTests
{
    private RanttOutput _ranttOutput;
    private Mock<ILogger<RanttOutput>> _loggerMock;
    private Mock<IMethodCallReporter> _reporterMock;
    private string _testOutputPath;

    [SetUp]
    public void Setup()
    {
        _testOutputPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_testOutputPath);

        _loggerMock = new Mock<ILogger<RanttOutput>>();
        _reporterMock = new Mock<IMethodCallReporter>();

        _ranttOutput = new RanttOutput(
            _loggerMock.Object,
            () => new EnhancedDataPostProcessor()
        );
        var parameters = RanttOutput.CreateParameters(_testOutputPath, orphanedNodeStrategy: OrphanedNodeStrategy.AttachToNearestAncestor);
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
    public async Task ExportRelationships_EnsuresRootNodeExists()
    {
        // Arrange
        var reportItems = new List<ReportItem>
        {
            CreateReportItem("1", "ROOT", "Method1"),
            CreateReportItem("2", "1", "Method2")
        };

        // Act
        await InitializeAndExportData(reportItems);

        // Assert
        var relationshipsFilePath = Path.Combine(_testOutputPath, "TestReporter", "TestReporter_Relationships.csv");
        Assert.That(File.Exists(relationshipsFilePath), Is.True);

        var relationshipsContent = await File.ReadAllTextAsync(relationshipsFilePath);
        Console.WriteLine($"Relationships content:\n{relationshipsContent}");

        Assert.That(relationshipsContent, Does.Contain("ROOT,1,"));
        Assert.That(relationshipsContent, Does.Contain("1,2,"));
    }

    [Test]
    public async Task ExportToCsv_WithDifferentOrphanedNodeStrategies_AppliesCorrectStrategy()
    {
        // Arrange
        var reportItems = new List<ReportItem> { CreateReportItem("1", "ROOT", "Method1"), CreateReportItem("2", "999", "OrphanedMethod") };

        // Act & Assert
        // Test RemoveOrphans strategy
        var removeOrphansParameters = RanttOutput.CreateParameters(_testOutputPath, orphanedNodeStrategy: OrphanedNodeStrategy.RemoveOrphans);
        _ranttOutput.SetParameters(removeOrphansParameters);

        await InitializeAndExportData(reportItems);

        var csvFilePath = Path.Combine(_testOutputPath, "TestReporter", "TestReporter.csv");
        var csvContent = await File.ReadAllTextAsync(csvFilePath);
        Console.WriteLine($"CSV content (RemoveOrphans):\n{csvContent}");
        Assert.That(csvContent, Does.Contain("Method1"));
        Assert.That(csvContent, Does.Not.Contain("OrphanedMethod"));

        // Test AttachToRoot strategy
        var attachToRootParameters = RanttOutput.CreateParameters(_testOutputPath, orphanedNodeStrategy: OrphanedNodeStrategy.AttachToRoot);
        _ranttOutput.SetParameters(attachToRootParameters);

        await InitializeAndExportData(reportItems);

        csvContent = await File.ReadAllTextAsync(csvFilePath);
        Console.WriteLine($"CSV content (AttachToRoot):\n{csvContent}");
        Assert.That(csvContent, Does.Contain("Method1"));
        Assert.That(csvContent, Does.Contain("OrphanedMethod"));
        Assert.That(csvContent, Does.Contain("ROOT,2,"));
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

        var parameters = RanttOutput.CreateParameters(_testOutputPath, orphanedNodeStrategy: OrphanedNodeStrategy.AttachToNearestAncestor);
        _ranttOutput.SetParameters(parameters);

        // Act
        await InitializeAndExportData(reportItems);

        // Assert
        var csvFilePath = Path.Combine(_testOutputPath, "TestReporter", "TestReporter.csv");
        Assert.That(File.Exists(csvFilePath), Is.True);

        var csvContent = await File.ReadAllTextAsync(csvFilePath);
        Console.WriteLine($"CSV content:\n{csvContent}");
        Assert.That(csvContent, Does.Contain("OrphanedMethod"));
        Assert.That(csvContent, Does.Contain("1,3,"));
    }

    private async Task InitializeAndExportData(List<ReportItem> reportItems)
    {
        var helper = new ReportOutputHelper();
        helper.Initialize(_reporterMock.Object);
        var methodCallStarts = new List<MethodCallStart>();

        foreach (var item in reportItems)
        {
            var methodCallStart = CreateMethodCallStart(item);
            methodCallStarts.Add(methodCallStart);
            helper.ProcessCallStackItem(methodCallStart);
        }

        var disposable = _ranttOutput.Initialize(_reporterMock.Object);
        foreach (var methodCallStart in methodCallStarts)
        {
            _ranttOutput.WriteItem(methodCallStart);
        }
        await disposable.DisposeAsync();
    }

    private ReportItem CreateReportItem(string id, string? parent, string methodName)
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

        if (item.Parent == "ROOT")
        {
            methodCallInfo.Parent = MethodCallInfo.CreateNull();
        }
        else if (!string.IsNullOrEmpty(item.Parent))
        {
            methodCallInfo.Parent = MethodCallInfo.Create(
                new MethodCallInfoPool(),
                null,
                typeof(RanttOutputPostProcessingTests),
                new TestMethodInfo("ParentMethod", typeof(RanttOutputPostProcessingTests)),
                Array.Empty<Type>(),
                item.Parent,
                new Dictionary<string, string>()
            );
        }

        return new MethodCallStart(methodCallInfo);
    }
}

#pragma warning disable CL0002
namespace Orc.Monitoring.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using Orc.Monitoring.MethodLifeCycleItems;
using Orc.Monitoring.Reporters;
using Orc.Monitoring.Reporters.ReportOutputs;
using Microsoft.Extensions.Logging;

[TestFixture]
public class RanttOutputPostProcessingTests
{
    private RanttOutput _ranttOutput;
    private Mock<IMethodCallReporter> _reporterMock;
    private string _testOutputPath;
    private TestLogger<RanttOutputPostProcessingTests> _logger;
    private ReportOutputHelper _reportOutputHelper;

    private const string RelationshipsFileName = "TestReporter_Relationships.csv";
    private const string CsvFileName = "TestReporter.csv";

    [SetUp]
    public void Setup()
    {
        _logger = new TestLogger<RanttOutputPostProcessingTests>();
        _testOutputPath = CreateTestOutputPath();
        _reporterMock = new Mock<IMethodCallReporter>();
        _ranttOutput = InitializeRanttOutput(OrphanedNodeStrategy.AttachToNearestAncestor);
        _reporterMock.Setup(r => r.FullName).Returns("TestReporter");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_testOutputPath))
        {
            try
            {
                Directory.Delete(_testOutputPath, true);
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, new EventId(), ex.Message, ex, (s, e) => s);
            }
        }
    }

    [Test]
    public async Task ExportRelationships_EnsuresRootNodeExists()
    {
        _logger.LogInformation("Starting ExportRelationships_EnsuresRootNodeExists test");
        var reportItems = new List<ReportItem>
        {
            CreateReportItem("1", "ROOT", "Method1"),
            CreateReportItem("2", "1", "Method2")
        };

        await InitializeAndExportData(reportItems);

        var relationshipsFilePath = Path.Combine(_testOutputPath, "TestReporter", RelationshipsFileName);
        Assert.That(File.Exists(relationshipsFilePath), Is.True, $"Relationships file does not exist: {relationshipsFilePath}");

        var relationshipsContent = await File.ReadAllTextAsync(relationshipsFilePath);
        Assert.That(relationshipsContent, Does.Contain("ROOT,1,"), "ROOT to Method1 relationship not found");
        Assert.That(relationshipsContent, Does.Contain("1,2,"), "Method1 to Method2 relationship not found");

        // Verify logger output
        Assert.That(_logger.LogMessages, Does.Contain("Starting ExportRelationships_EnsuresRootNodeExists test"));
    }

    [Test]
    public async Task ExportToCsv_WithDifferentOrphanedNodeStrategies_AppliesCorrectStrategy()
    {
        _logger.LogInformation("Starting ExportToCsv_WithDifferentOrphanedNodeStrategies_AppliesCorrectStrategy test");
        var reportItems = new List<ReportItem>
        {
            CreateReportItem("1", "ROOT", "Method1"),
            CreateReportItem("2", "999", "OrphanedMethod")
        };

        // Test RemoveOrphans strategy
        await InitializeAndExportDataWithStrategy(reportItems, OrphanedNodeStrategy.RemoveOrphans);

        var csvFilePath = Path.Combine(_testOutputPath, "TestReporter", CsvFileName);
        var csvContent = await File.ReadAllTextAsync(csvFilePath);
        _logger.LogInformation($"CSV content (RemoveOrphans):\n{csvContent}");
        Assert.That(csvContent, Does.Contain("Method1"), "Method1 should be present");
        Assert.That(csvContent, Does.Not.Contain("OrphanedMethod"), "OrphanedMethod should not be present");

        // Test AttachToRoot strategy
        await InitializeAndExportDataWithStrategy(reportItems, OrphanedNodeStrategy.AttachToRoot);

        csvContent = await File.ReadAllTextAsync(csvFilePath);
        _logger.LogInformation($"CSV content (AttachToRoot):\n{csvContent}");
        Assert.That(csvContent, Does.Contain("Method1"), "Method1 should be present");
        Assert.That(csvContent, Does.Contain("OrphanedMethod"), "OrphanedMethod should be present");
        Assert.That(csvContent, Does.Contain("2,ROOT,"), "OrphanedMethod should be attached to ROOT");

        // Verify logger output
        Assert.That(_logger.LogMessages, Does.Contain("Starting ExportToCsv_WithDifferentOrphanedNodeStrategies_AppliesCorrectStrategy test"));
    }

    [Test]
    public async Task ExportToCsv_WithOrphanedNodes_AppliesPostProcessing()
    {
        _logger.LogInformation("Starting ExportToCsv_WithOrphanedNodes_AppliesPostProcessing test");
        var reportItems = new List<ReportItem>
        {
            CreateReportItem("1", "ROOT", "Method1"),
            CreateReportItem("2", "1", "Method2"),
            CreateReportItem("3", "999", "OrphanedMethod")
        };

        _ranttOutput = InitializeRanttOutput(OrphanedNodeStrategy.AttachToNearestAncestor);

        await InitializeAndExportData(reportItems);

        var csvFilePath = Path.Combine(_testOutputPath, "TestReporter", CsvFileName);
        Assert.That(File.Exists(csvFilePath), Is.True, $"CSV file does not exist: {csvFilePath}");

        var csvContent = await File.ReadAllTextAsync(csvFilePath);
        _logger.LogInformation($"CSV content:\n{csvContent}");
        Assert.That(csvContent, Does.Contain("OrphanedMethod"), "OrphanedMethod should be present");
        Assert.That(csvContent, Does.Contain("3,2,"), "Orphaned node should be attached to node 2");
        Assert.That(csvContent, Does.Contain("2,1,"), "Node 2 should still be a child of node 1");
        Assert.That(csvContent, Does.Contain("1,ROOT,"), "Node 1 should still be a child of ROOT");

        // Verify logger output
        Assert.That(_logger.LogMessages, Does.Contain("Starting ExportToCsv_WithOrphanedNodes_AppliesPostProcessing test"));
    }

    private async Task InitializeAndExportData(List<ReportItem> reportItems)
    {
        _logger.LogInformation("Initializing and exporting data");
        var helper = new ReportOutputHelper(_logger.CreateLogger<ReportOutputHelper>());
        helper.Initialize(_reporterMock.Object);
        var methodCallStarts = new List<MethodCallStart>();

        foreach (var item in reportItems)
        {
            var methodCallStart = CreateMethodCallStart(item);
            methodCallStarts.Add(methodCallStart);
            helper.ProcessCallStackItem(methodCallStart);
        }

        await using var disposable = _ranttOutput.Initialize(_reporterMock.Object);
        foreach (var methodCallStart in methodCallStarts)
        {
            _ranttOutput.WriteItem(methodCallStart);
        }

        _logger.LogInformation("Data export completed");
    }

    private async Task InitializeAndExportDataWithStrategy(List<ReportItem> reportItems, OrphanedNodeStrategy strategy)
    {
        _ranttOutput = InitializeRanttOutput(strategy);
        await InitializeAndExportData(reportItems);
    }

    private ReportItem CreateReportItem(string id, string? parent, string methodName)
    {
        return new ReportItem
        {
            Id = id,
            Parent = parent,
            MethodName = methodName,
            StartTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
            EndTime = DateTime.Now.AddSeconds(1).ToString("yyyy-MM-dd HH:mm:ss.fff"),
            FullName = $"RanttOutputPostProcessingTests.{methodName}()",
            ClassName = "RanttOutputPostProcessingTests",
            ThreadId = "14",
            ParentThreadId = parent == "ROOT" ? "0" : "14",
            Report = "TestReporter"
        };
    }

    private MethodCallStart CreateMethodCallStart(ReportItem item)
    {
        var methodInfo = new TestMethodInfo(item.MethodName, typeof(RanttOutputPostProcessingTests));
        var methodCallInfo = MethodCallInfo.Create(
            new MethodCallInfoPool(_logger.CreateLogger<MethodCallInfoPool>()),
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
                new MethodCallInfoPool(_logger.CreateLogger<MethodCallInfoPool>()),
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

    private string CreateTestOutputPath()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(path);
        return path;
    }

    private RanttOutput InitializeRanttOutput(OrphanedNodeStrategy strategy)
    {
        _reportOutputHelper = new ReportOutputHelper(_logger.CreateLogger<ReportOutputHelper>());
        var output = new RanttOutput(
            _logger.CreateLogger<RanttOutput>(),
            () => new EnhancedDataPostProcessor(_logger.CreateLogger<EnhancedDataPostProcessor>()),
            _reportOutputHelper,
            (outputFolder) => new MethodOverrideManager(outputFolder, _logger.CreateLogger<MethodOverrideManager>()));
        var parameters = RanttOutput.CreateParameters(_testOutputPath, orphanedNodeStrategy: strategy);
        output.SetParameters(parameters);
        return output;
    }
}

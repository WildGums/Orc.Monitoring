#pragma warning disable CL0002
namespace Orc.Monitoring.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using MethodLifeCycleItems;
using Reporters;
using Reporters.ReportOutputs;
using Microsoft.Extensions.Logging;
using TestUtilities.Logging;
using TestUtilities.Mocks;
using TestUtilities.TestHelpers;
using Orc.Monitoring.TestUtilities;

[TestFixture]
public class RanttOutputPostProcessingTests
{
    private RanttOutput _ranttOutput;
    private Mock<IMethodCallReporter> _reporterMock;
    private string _testOutputPath;
    private TestLogger<RanttOutputPostProcessingTests> _logger;
    private TestLoggerFactory<RanttOutputPostProcessingTests> _loggerFactory;
    private ReportOutputHelper _reportOutputHelper;
    private Mock<IEnhancedDataPostProcessor> _mockPostProcessor;
    private MethodCallInfoPool _methodCallInfoPool;
    private IMonitoringController _monitoringController;
    private InMemoryFileSystem _fileSystem;
    private CsvUtils _csvUtils;
    private ReportArchiver _reportArchiver;
    private const string RelationshipsFileName = "TestReporter_Relationships.csv";
    private const string CsvFileName = "TestReporter.csv";

    [SetUp]
    public void Setup()
    {
        _logger = new TestLogger<RanttOutputPostProcessingTests>();
        _loggerFactory = new TestLoggerFactory<RanttOutputPostProcessingTests>(_logger);
        _fileSystem = new InMemoryFileSystem(_loggerFactory);
        _csvUtils = TestHelperMethods.CreateCsvUtils(_fileSystem, _loggerFactory);
        _reportArchiver = new ReportArchiver(_fileSystem, _loggerFactory);
        _testOutputPath = CreateTestOutputPath();
        _reporterMock = new Mock<IMethodCallReporter>();
        _mockPostProcessor = new Mock<IEnhancedDataPostProcessor>();
        _monitoringController = new MonitoringController(_loggerFactory);
        _methodCallInfoPool = new MethodCallInfoPool(_monitoringController, _loggerFactory);
        _ranttOutput = InitializeRanttOutput();
        _reporterMock.Setup(r => r.FullName).Returns("TestReporter");
    }

    [TearDown]
    public void TearDown()
    {
        _fileSystem.Dispose();
        if (_fileSystem.DirectoryExists(_testOutputPath))
        {
            try
            {
                _fileSystem.DeleteDirectory(_testOutputPath, true);
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, new EventId(), ex.Message, ex, (s, _) => s);
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

        _mockPostProcessor.Setup(p => p.PostProcessData(It.IsAny<List<ReportItem>>()))
            .Returns(reportItems);

        await InitializeAndExportData(reportItems);

        var relationshipsFilePath = _fileSystem.Combine(_testOutputPath, "TestReporter", RelationshipsFileName);
        Assert.That(_fileSystem.FileExists(relationshipsFilePath), Is.True, $"Relationships file does not exist: {relationshipsFilePath}");

        var relationshipsContent = await _fileSystem.ReadAllTextAsync(relationshipsFilePath);
        Assert.That(relationshipsContent, Does.Contain("ROOT,1,"), "ROOT to Method1 relationship not found");
        Assert.That(relationshipsContent, Does.Contain("1,2,"), "Method1 to Method2 relationship not found");

        // Verify logger output
        Assert.That(_logger.LogMessages, Does.Contain("Starting ExportRelationships_EnsuresRootNodeExists test"));
    }

    [Test]
    public async Task ExportToCsv_AppliesPostProcessing()
    {
        _logger.LogInformation("Starting ExportToCsv_AppliesPostProcessing test");
        var reportItems = new List<ReportItem>
        {
            CreateReportItem("1", "ROOT", "Method1"),
            CreateReportItem("2", "1", "Method2"),
            CreateReportItem("3", "999", "OrphanedMethod")
        };

        var processedItems = new List<ReportItem>
        {
            CreateReportItem("1", "ROOT", "Method1"),
            CreateReportItem("2", "1", "Method2")
        };

        _mockPostProcessor.Setup(p => p.PostProcessData(It.IsAny<List<ReportItem>>()))
            .Returns(processedItems);

        await InitializeAndExportData(reportItems);

        var csvFilePath = _fileSystem.Combine(_testOutputPath, "TestReporter", CsvFileName);
        Assert.That(_fileSystem.FileExists(csvFilePath), Is.True, $"CSV file does not exist: {csvFilePath}");

        var csvContent = await _fileSystem.ReadAllTextAsync(csvFilePath);
        _logger.LogInformation($"CSV content:\n{csvContent}");
        Assert.That(csvContent, Does.Contain("Method1"), "Method1 should be present");
        Assert.That(csvContent, Does.Contain("Method2"), "Method2 should be present");
        Assert.That(csvContent, Does.Not.Contain("OrphanedMethod"), "OrphanedMethod should not be present");

        // Verify logger output
        Assert.That(_logger.LogMessages, Does.Contain("Starting ExportToCsv_AppliesPostProcessing test"));
    }

    private async Task InitializeAndExportData(List<ReportItem> reportItems)
    {
        _logger.LogInformation("Initializing and exporting data");
        var helper = new ReportOutputHelper(_loggerFactory, new ReportItemFactory(_loggerFactory));
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
        var methodCallInfo = _methodCallInfoPool.Rent(
            null,
            typeof(RanttOutputPostProcessingTests),
            methodInfo,
            Array.Empty<Type>(),
            item.Id,
            new Dictionary<string, string>()
        );

        if (item.Parent == "ROOT")
        {
            methodCallInfo.Parent = _methodCallInfoPool.GetNull();
        }
        else if (!string.IsNullOrEmpty(item.Parent))
        {
            methodCallInfo.Parent = _methodCallInfoPool.Rent(
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
        var path = _fileSystem.Combine(_fileSystem.GetTempPath(), _fileSystem.GetRandomFileName());
        _fileSystem.CreateDirectory(path);
        return path;
    }

    private RanttOutput InitializeRanttOutput()
    {
        _reportOutputHelper = new ReportOutputHelper(_loggerFactory, new ReportItemFactory(_loggerFactory));
        var output = new RanttOutput(
            MonitoringLoggerFactory.Instance,
            () => _mockPostProcessor.Object,
            _reportOutputHelper,
            (outputFolder) => new MethodOverrideManager(outputFolder, _loggerFactory, _fileSystem, _csvUtils), 
            _fileSystem,
            _reportArchiver, new ReportItemFactory(_loggerFactory));
        var parameters = RanttOutput.CreateParameters(_testOutputPath);
        output.SetParameters(parameters);
        return output;
    }
}

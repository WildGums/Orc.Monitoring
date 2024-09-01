#pragma warning disable CL0002
namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using Orc.Monitoring.Reporters;
using Orc.Monitoring.Reporters.ReportOutputs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IO;
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
    private TestLoggerFactory<RanttOutputTests> _loggerFactory;
    private IMonitoringController _monitoringController;
    private MethodCallInfoPool _methodCallInfoPool;
    private Mock<IEnhancedDataPostProcessor> _mockPostProcessor;
    private Mock<IFileSystem> _mockFileSystem;

    [SetUp]
    public void Setup()
    {
        _logger = new TestLogger<RanttOutputTests>();
        _loggerFactory = new TestLoggerFactory<RanttOutputTests>(_logger);
        _monitoringController = new MonitoringController(_loggerFactory, () => new EnhancedDataPostProcessor(_loggerFactory));
        _methodCallInfoPool = new MethodCallInfoPool(_monitoringController, _loggerFactory);
        _testFolderPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        _mockReporter = new MockReporter(_loggerFactory) { Name = "TestReporter", FullName = "TestReporter" };
        _mockPostProcessor = new Mock<IEnhancedDataPostProcessor>();
        _mockFileSystem = new Mock<IFileSystem>();

        _mockFileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
        _mockFileSystem.Setup(fs => fs.CreateDirectory(It.IsAny<string>()));

        _ranttOutput = new RanttOutput(_loggerFactory,
            () => _mockPostProcessor.Object,
            new ReportOutputHelper(_loggerFactory),
            (outputFolder) => new MethodOverrideManager(outputFolder, _loggerFactory),
            _mockFileSystem.Object);
        var parameters = RanttOutput.CreateParameters(_testFolderPath);
        _ranttOutput.SetParameters(parameters);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_testFolderPath))
        {
            try
            {
                Directory.Delete(_testFolderPath, true);
            }
            catch (UnauthorizedAccessException)
            {
                // Log the error and continue
                _logger.LogError($"Unable to delete test folder: {_testFolderPath}");
            }
        }
    }

    [Test]
    public async Task WriteItem_CorrectlyGeneratesRelationships()
    {
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

        _mockPostProcessor.Setup(p => p.PostProcessData(It.IsAny<List<ReportItem>>()))
            .Returns((List<ReportItem> items) => items);

        await disposable.DisposeAsync();

        var relationshipsFilePath = Path.Combine(_testFolderPath, "TestReporter", "TestReporter_Relationships.csv");
        Assert.That(File.Exists(relationshipsFilePath), Is.True, "Relationships file should exist");

        var relationshipsContent = await File.ReadAllTextAsync(relationshipsFilePath);
        var lines = relationshipsContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.That(lines.Length, Is.GreaterThan(1), "Relationships file should have more than just the header");
        Assert.That(lines.Any(l => l.StartsWith($"{parentMethodInfo.Id},{childMethodInfo.Id}")), Is.True,
            $"Relationship between parent and child should be present. Expected: {parentMethodInfo.Id},{childMethodInfo.Id}");

        _mockPostProcessor.Verify(p => p.PostProcessData(It.IsAny<List<ReportItem>>()), Times.Exactly(2));
    }

    [Test]
    public async Task ExportToCsv_AppliesPostProcessing()
    {
        var disposable = _ranttOutput.Initialize(_mockReporter);

        var methodInfo1 = CreateMethodCallInfo("Method1", null);
        var methodInfo2 = CreateMethodCallInfo("Method2", methodInfo1);

        _ranttOutput.WriteItem(new MethodCallStart(methodInfo1));
        _ranttOutput.WriteItem(new MethodCallStart(methodInfo2));
        _ranttOutput.WriteItem(new MethodCallEnd(methodInfo2));
        _ranttOutput.WriteItem(new MethodCallEnd(methodInfo1));

        _mockPostProcessor.Setup(p => p.PostProcessData(It.IsAny<List<ReportItem>>()))
            .Returns((List<ReportItem> items) =>
            {
                items.First(i => i.Id == methodInfo1.Id).Parent = null;
                return items;
            });

        await disposable.DisposeAsync();

        var csvFilePath = Path.Combine(_testFolderPath, "TestReporter", "TestReporter.csv");
        Assert.That(File.Exists(csvFilePath), Is.True, "CSV file should exist");

        var csvContent = await File.ReadAllTextAsync(csvFilePath);
        var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.That(lines[1], Does.StartWith($"{methodInfo1.Id},"), "Method1 should have an empty parent");
        Assert.That(lines[2], Does.StartWith($"{methodInfo2.Id},{methodInfo1.Id}"), "Method2 should be a child of Method1");

        _mockPostProcessor.Verify(p => p.PostProcessData(It.IsAny<List<ReportItem>>()), Times.Exactly(2));
    }

    [Test]
    public async Task Initialize_WithReadOnlyFolder_ThrowsUnauthorizedAccessException()
    {
        var readOnlyFolder = Path.Combine(_testFolderPath, "ReadOnly");
        Directory.CreateDirectory(readOnlyFolder);
        File.SetAttributes(readOnlyFolder, FileAttributes.ReadOnly);

        var parameters = RanttOutput.CreateParameters(readOnlyFolder);
        _ranttOutput.SetParameters(parameters);

        Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
        {
            await using var _ = _ranttOutput.Initialize(_mockReporter);
        });
    }

    [Test]
    public async Task WriteItem_WithCorruptData_HandlesGracefully()
    {
        var disposable = _ranttOutput.Initialize(_mockReporter);

        var corruptMethodCallInfo = CreateMethodCallInfo("CorruptMethod", null);
        corruptMethodCallInfo.Parameters = new Dictionary<string, string> { { "CorruptKey", "\0InvalidValue" } };

        var methodCallStart = new MethodCallStart(corruptMethodCallInfo);
        Assert.DoesNotThrow(() => _ranttOutput.WriteItem(methodCallStart), "WriteItem should handle corrupt data gracefully");

        await disposable.DisposeAsync();
    }

    [Test]
    public async Task Dispose_WhenFileIsLocked_HandlesGracefully()
    {
        var disposable = _ranttOutput.Initialize(_mockReporter);

        var filePath = Path.Combine(_testFolderPath, "TestReporter", "TestReporter.csv");

        // Ensure directory exists
        Directory.CreateDirectory(Path.GetDirectoryName(filePath));

        // Lock the file
        using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
        {
            // Attempt to dispose while the file is locked
            await disposable.DisposeAsync();
        }

        // Verify that the file still exists
        Assert.That(File.Exists(filePath), Is.True, "CSV file should still exist after failed dispose");
    }

    [Test]
    public async Task WriteItem_WhenDiskIsFull_HandlesGracefully()
    {
        _mockFileSystem.Setup(fs => fs.AppendAllText(It.IsAny<string>(), It.IsAny<string>()))
            .Throws(new IOException("Disk full"));

        var disposable = _ranttOutput.Initialize(_mockReporter);

        var methodCallInfo = CreateMethodCallInfo("TestMethod", null);
        var methodCallStart = new MethodCallStart(methodCallInfo);

        Assert.DoesNotThrow(() => _ranttOutput.WriteItem(methodCallStart), "WriteItem should handle disk full error gracefully");

        await disposable.DisposeAsync();
    }

    [Test]
    public async Task ExportToRantt_WithInvalidXmlCharacters_HandlesGracefully()
    {
        var disposable = _ranttOutput.Initialize(_mockReporter);

        var methodCallInfo = CreateMethodCallInfo("Method<with>Invalid&Xml\"Chars", null);
        var methodCallStart = new MethodCallStart(methodCallInfo);
        _ranttOutput.WriteItem(methodCallStart);
        _ranttOutput.WriteItem(new MethodCallEnd(methodCallInfo));

        await disposable.DisposeAsync();

        var ranttFilePath = Path.Combine(_testFolderPath, "TestReporter", "TestReporter.rprjx");
        Assert.That(File.Exists(ranttFilePath), Is.True, "Rantt project file should be created");

        var ranttContent = await File.ReadAllTextAsync(ranttFilePath);
        Assert.That(ranttContent, Does.Not.Contain("<"), "Invalid XML characters should be handled");
        Assert.That(ranttContent, Does.Not.Contain(">"), "Invalid XML characters should be handled");
        Assert.That(ranttContent, Does.Not.Contain("&"), "Invalid XML characters should be handled");
        Assert.That(ranttContent, Does.Not.Contain("\""), "Invalid XML characters should be handled");
    }


    [Test]
    public async Task CreateTimestampedFolderCopy_WhenDestinationExists_HandlesGracefully()
    {
        var disposable = _ranttOutput.Initialize(_mockReporter);

        // Setup mock file system
        _mockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(true);
        _mockFileSystem.Setup(fs => fs.ReadAllText(It.IsAny<string>())).Returns("Test content");
        _mockFileSystem.Setup(fs => fs.GetFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()))
            .Returns(new[] { Path.Combine(_testFolderPath, "Archived", "TestReporter_20230101_000000", "TestFile.txt") });

        await disposable.DisposeAsync();

        // Verify that the original file still exists
        _mockFileSystem.Verify(fs => fs.FileExists(It.Is<string>(s => s.EndsWith("TestFile.txt"))), Times.AtLeastOnce);

        // Verify that an archived copy was attempted
        _mockFileSystem.Verify(fs => fs.GetFiles(It.IsAny<string>(), "TestFile.txt", SearchOption.AllDirectories), Times.Once);
    }

    private MethodCallInfo CreateMethodCallInfo(string methodName, MethodCallInfo parent)
    {
        var methodInfo = new TestMethodInfo(methodName, typeof(RanttOutputTests));
        var methodCallInfo = _methodCallInfoPool.Rent(
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

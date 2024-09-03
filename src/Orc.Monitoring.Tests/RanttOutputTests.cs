#pragma warning disable CL0001
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
#pragma warning disable IDISP006
    private InMemoryFileSystem _fileSystem;
#pragma warning restore IDISP006
    private ReportArchiver _reportArchiver;
    private CsvUtils _csvUtils;

    private const string TestReporterName = "TestReporter";
    private const string RelationshipsFileName = "TestReporter_Relationships.csv";
    private const string CsvFileName = "TestReporter.csv";
    private const string RanttFileName = "TestReporter.rprjx";
    private const int ExpectedCsvLineCount = 3;

    [SetUp]
    public void Setup()
    {
        InitializeLogger();
        InitializeDependencies();
        InitializeRanttOutput();

        _monitoringController.Enable();
    }

    private void InitializeLogger()
    {
        _logger = new TestLogger<RanttOutputTests>();
        _loggerFactory = new TestLoggerFactory<RanttOutputTests>(_logger);
        _loggerFactory.EnableLoggingFor<RanttOutput>();
        _loggerFactory.EnableLoggingFor<ReportOutputHelper>();
        _loggerFactory.EnableLoggingFor<InMemoryFileSystem>();
        _loggerFactory.EnableLoggingFor<CsvReportOutput>();
    }

    private void InitializeDependencies()
    {
        _monitoringController = new MonitoringController(_loggerFactory, () => new EnhancedDataPostProcessor(_loggerFactory));
        _methodCallInfoPool = new MethodCallInfoPool(_monitoringController, _loggerFactory);
        _testFolderPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        _mockReporter = new MockReporter(_loggerFactory) { Name = TestReporterName, FullName = TestReporterName };
#pragma warning disable IDISP003
        _fileSystem = new InMemoryFileSystem(_loggerFactory);
#pragma warning restore IDISP003
        _csvUtils = new CsvUtils(_fileSystem);
        _reportArchiver = new ReportArchiver(_fileSystem, _loggerFactory);
    }

    private void InitializeRanttOutput()
    {
        _ranttOutput = new RanttOutput(_loggerFactory,
            () => new EnhancedDataPostProcessor(_loggerFactory),
            new ReportOutputHelper(_loggerFactory),
            (outputFolder) => new MethodOverrideManager(outputFolder, _loggerFactory, _fileSystem, _csvUtils),
            _fileSystem,
            _reportArchiver);
        var parameters = RanttOutput.CreateParameters(_testFolderPath);
        _ranttOutput.SetParameters(parameters);
    }

    [TearDown]
    public void TearDown()
    {
        _fileSystem.Dispose();
        if (_fileSystem.DirectoryExists(_testFolderPath))
        {
            _fileSystem.DeleteDirectory(_testFolderPath, true);
        }
    }

    [Test]
    public async Task WriteItem_ShouldGenerateCorrectParentChildRelationships()
    {
        var disposable = _ranttOutput.Initialize(_mockReporter);

        var parentMethodInfo = CreateMethodCallInfo("ParentMethod", null);
        WriteMethodLifecycle(parentMethodInfo);

        var childMethodInfo = CreateMethodCallInfo("ChildMethod", parentMethodInfo);
        WriteMethodLifecycle(childMethodInfo);

        await disposable.DisposeAsync();

        var relationshipsFilePath = GetFilePath(RelationshipsFileName);
        AssertFileExists(relationshipsFilePath);

        var relationshipsContent = await _fileSystem.ReadAllTextAsync(relationshipsFilePath);
        _logger.LogInformation($"Relationships content:\n{relationshipsContent}");

        var lines = relationshipsContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.That(lines.Length, Is.GreaterThan(1), "Relationships file should have more than just the header");
        Assert.That(lines.Any(l => l.StartsWith($"{parentMethodInfo.Id},{childMethodInfo.Id}")), Is.True,
            $"Relationship between parent and child should be present. Expected: {parentMethodInfo.Id},{childMethodInfo.Id}");
    }

    [Test]
    public async Task ExportToCsv_ShouldApplyOverridesCorrectly()
    {
        _logger.LogInformation("Starting ExportToCsv_ShouldApplyOverridesCorrectly test");

        // Setup override file
        var overrideContent = "FullName,CustomColumn\nRanttOutputTests.Method1,CustomValue";
        _fileSystem.WriteAllText(Path.Combine(_testFolderPath, "TestReporter", "method_overrides.csv"), overrideContent);

        var disposable = _ranttOutput.Initialize(_mockReporter);

        var methodInfo1 = CreateMethodCallInfo("Method1", null);
        _logger.LogInformation($"Created Method1: {methodInfo1.Id}");
        var methodInfo2 = CreateMethodCallInfo("Method2", methodInfo1);
        _logger.LogInformation($"Created Method2: {methodInfo2.Id}");

        WriteMethodLifecycle(methodInfo1);
        WriteMethodLifecycle(methodInfo2);

        _logger.LogInformation($"ReportItems count before export: {_ranttOutput.GetDebugInfo()}");

        await disposable.DisposeAsync();

        var csvFilePath = Path.Combine(_testFolderPath, "TestReporter", "TestReporter.csv");
        Assert.That(_fileSystem.FileExists(csvFilePath), Is.True, "CSV file should be created");

        var csvContent = await _fileSystem.ReadAllTextAsync(csvFilePath);
        _logger.LogInformation($"CSV Content:\n{csvContent}");
        var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        _logger.LogInformation($"Number of non-empty lines: {lines.Length}");

        Assert.That(lines.Length, Is.EqualTo(3), "Should have header and two data lines");

        var headers = lines[0].Split(',').Select(h => h.Trim()).ToArray();
        Assert.That(headers, Does.Contain("Id").And.Contain("MethodName").And.Contain("CustomColumn"),
            "Header should contain expected columns");

        var dataRow = lines[1].Split(',');
        var customColumnIndex = Array.IndexOf(headers, "CustomColumn");
        Assert.That(dataRow[customColumnIndex].Trim(), Is.EqualTo("CustomValue"),
            "Custom column should contain the override value");

        var secondDataRow = lines[2].Split(',');
        Assert.That(secondDataRow[customColumnIndex].Trim(), Is.Empty,
            "Second row should not have a value for CustomColumn");
    }

    [Test]
    [Ignore("Not important at the moment")]
    public async Task Initialize_ShouldThrowUnauthorizedAccessException_WhenFolderIsReadOnly()
    {
        var readOnlyFolder = CreateReadOnlyTestFolder();
        var parameters = RanttOutput.CreateParameters(readOnlyFolder);
        _ranttOutput.SetParameters(parameters);

        Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
        {
            await using var _ = _ranttOutput.Initialize(_mockReporter);
        });
    }

    [Test]
    public async Task WriteItem_ShouldHandleCorruptDataGracefully()
    {
        var disposable = _ranttOutput.Initialize(_mockReporter);

        var corruptMethodCallInfo = CreateMethodCallInfo("CorruptMethod", null);
        corruptMethodCallInfo.Parameters = new Dictionary<string, string> { { "CorruptKey", "\0InvalidValue" } };

        var methodCallStart = new MethodCallStart(corruptMethodCallInfo);
        Assert.DoesNotThrow(() => _ranttOutput.WriteItem(methodCallStart), "WriteItem should handle corrupt data gracefully");

        await disposable.DisposeAsync();
    }

    [Test]
    public async Task ExportToRantt_ShouldHandleInvalidXmlCharactersGracefully()
    {
        var methodName = "Method<with>Invalid&Xml\"Chars";
        var methodCallInfo = CreateMethodCallInfo(methodName, null);

        await using (var disposable = _ranttOutput.Initialize(_mockReporter))
        {
            _ranttOutput.WriteItem(new MethodCallStart(methodCallInfo));
            _ranttOutput.WriteItem(new MethodCallEnd(methodCallInfo));
        }

        var csvFilePath = Path.Combine(_testFolderPath, "TestReporter", "TestReporter.csv");
        Assert.That(_fileSystem.FileExists(csvFilePath), Is.True, "CSV file should be created");

        var csvContent = await _fileSystem.ReadAllTextAsync(csvFilePath);
        _logger.LogInformation($"CSV Content:\n{csvContent}");

        // Check for the correct escaping in the MethodName column
        Assert.That(csvContent, Does.Contain("\"Method<with>Invalid&Xml\"\"Chars\""),
            "Method name should be properly escaped in CSV");

        // Add a more flexible check
        Assert.That(csvContent, Does.Contain("Method<with>Invalid&Xml").And.Contain("Chars"),
            "CSV should contain the method name, regardless of exact escaping");

        // Check the FullName column
        Assert.That(csvContent, Does.Contain("\"RanttOutputTests.Method<with>Invalid&Xml\"\"Chars\""),
            "Full name should be properly escaped in CSV");

        var csvLines = await _fileSystem.ReadAllLinesAsync(csvFilePath);
        Assert.That(csvLines.Length, Is.EqualTo(2), "CSV should contain header and one data line");

        var ranttFilePath = Path.Combine(_testFolderPath, "TestReporter", "TestReporter.rprjx");
        Assert.That(_fileSystem.FileExists(ranttFilePath), Is.True, "Rantt project file should be created");

        var ranttContent = await _fileSystem.ReadAllTextAsync(ranttFilePath);
        Assert.That(ranttContent, Does.Not.Contain("<with>"),
            "Unescaped XML content should not be present in Rantt project file");
        Assert.That(ranttContent, Does.Not.Contain("Invalid&Xml\"Chars"),
            "Unescaped XML content should not be present in Rantt project file");
    }

    private MethodCallInfo CreateMethodCallInfo(string methodName, MethodCallInfo? parent)
    {
        var methodInfo = new TestMethodInfo(methodName, typeof(RanttOutputTests));
        var id = Guid.NewGuid().ToString();
        var methodCallInfo = _methodCallInfoPool.Rent(
            null,
            typeof(RanttOutputTests),
            methodInfo,
            Array.Empty<Type>(),
            id,
            new Dictionary<string, string>()
        );
        methodCallInfo.Parent = parent;
        methodCallInfo.MethodName = methodName;
        methodCallInfo.Id = id;

        _logger.LogInformation($"Created MethodCallInfo: Id={methodCallInfo.Id}, MethodName={methodCallInfo.MethodName}, ParentId={parent?.Id ?? "ROOT"}, IsNull={methodCallInfo.IsNull}");

        return methodCallInfo;
    }

    private void WriteMethodLifecycle(MethodCallInfo methodCallInfo)
    {
        _logger.LogInformation($"Writing lifecycle for {methodCallInfo.MethodName} (Id: {methodCallInfo.Id})");
        _ranttOutput.WriteItem(new MethodCallStart(methodCallInfo));
        _ranttOutput.WriteItem(new MethodCallEnd(methodCallInfo));
    }

    private string GetFilePath(string fileName)
    {
        return Path.Combine(_testFolderPath, TestReporterName, fileName);
    }

    private void AssertFileExists(string filePath)
    {
        Assert.That(_fileSystem.FileExists(filePath), Is.True, $"{filePath} should exist");
    }

    private string CreateReadOnlyTestFolder()
    {
        var readOnlyFolder = Path.Combine(_testFolderPath, "ReadOnly");
        _fileSystem.CreateDirectory(readOnlyFolder);
        _fileSystem.SetAttributes(readOnlyFolder, FileAttributes.ReadOnly);
        return readOnlyFolder;
    }
}

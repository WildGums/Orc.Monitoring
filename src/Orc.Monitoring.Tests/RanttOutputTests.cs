#pragma warning disable CL0001
#pragma warning disable CL0002
namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using Reporters;
using Reporters.ReportOutputs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MethodLifeCycleItems;
using TestUtilities.Logging;
using TestUtilities.Mocks;
using TestUtilities.TestHelpers;

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
    private string _testOutputPath;

    private const string TestReporterName = "TestReporter";
    private const string RelationshipsFileName = "TestReporter_Relationships.csv";
    private const string CsvFileName = "TestReporter.csv";

    [SetUp]
    public void Setup()
    {
        InitializeLogger();

        _logger.LogInformation("___");
        _logger.LogInformation("Starting RanttOutputTests setup");

        InitializeDependencies();

        _testOutputPath = _fileSystem.GetTempPath();

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
        _loggerFactory.EnableLoggingFor<EnhancedDataPostProcessor>();
    }

    private void InitializeDependencies()
    {
        _monitoringController = new MonitoringController(_loggerFactory);
        _methodCallInfoPool = new MethodCallInfoPool(_monitoringController, _loggerFactory);
        _mockReporter = new MockReporter(_loggerFactory) { Name = TestReporterName, FullName = TestReporterName };
#pragma warning disable IDISP003
        _fileSystem = new InMemoryFileSystem(_loggerFactory);
#pragma warning restore IDISP003
        _testFolderPath = _fileSystem.Combine(_fileSystem.GetTempPath(), _fileSystem.GetRandomFileName());
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
        _logger.LogInformation("Starting RanttOutputTests teardown");
        _fileSystem.Dispose();
        if (_fileSystem.DirectoryExists(_testFolderPath))
        {
            _fileSystem.DeleteDirectory(_testFolderPath, true);
        }

        _logger.LogInformation("Finished RanttOutputTests teardown");
        _logger.LogInformation("___");
    }

    [Test]
    public async Task WriteItem_ShouldGenerateCorrectParentChildRelationships()
    {
        _logger.LogInformation("Starting WriteItem_ShouldGenerateCorrectParentChildRelationships test");
        var disposable = _ranttOutput.Initialize(_mockReporter);

        var parentMethodInfo = CreateMethodCallInfo("ParentMethod", null);
        _logger.LogInformation($"Created parent method: Id={parentMethodInfo.Id}, MethodName={parentMethodInfo.MethodName}");
        WriteMethodLifecycle(parentMethodInfo);

        var childMethodInfo = CreateMethodCallInfo("ChildMethod", parentMethodInfo);
        _logger.LogInformation($"Created child method: Id={childMethodInfo.Id}, MethodName={childMethodInfo.MethodName}, Parent={childMethodInfo.Parent?.Id}");
        WriteMethodLifecycle(childMethodInfo);

        await disposable.DisposeAsync();

        var relationshipsFilePath = GetFilePath(RelationshipsFileName);
        AssertFileExists(relationshipsFilePath);

        var relationshipsContent = await _fileSystem.ReadAllTextAsync(relationshipsFilePath);
        _logger.LogInformation($"Relationships content:\n{relationshipsContent}");

        var lines = relationshipsContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.That(lines.Length, Is.GreaterThan(1), "Relationships file should have more than just the header");

        var dataLine = lines.Skip(1).FirstOrDefault()?.Trim('\r');
        Assert.That(dataLine, Is.Not.Null, "Relationships file should contain at least one data line");
        _logger.LogInformation($"Relationship data line: {dataLine}");

        var parts = dataLine.Split(',');
        Assert.That(parts.Length, Is.EqualTo(3), "Relationship line should have 3 parts: From, To, RelationType");

        Assert.That(parts[0], Is.EqualTo("ROOT"), "From should be ROOT");
        Assert.That(parts[1], Is.EqualTo(childMethodInfo.Id), "To should be the child method ID");
        Assert.That(parts[2], Is.EqualTo("Regular"), "RelationType should be Regular");

        // Additional checks
        var csvFilePath = GetFilePath(CsvFileName);
        AssertFileExists(csvFilePath);
        var csvContent = await _fileSystem.ReadAllTextAsync(csvFilePath);
        _logger.LogInformation($"CSV content:\n{csvContent}");
    }

    [Test]
    public async Task ExportToCsv_ShouldApplyOverridesCorrectly()
    {
        // Arrange
        var overrideContent = "FullName,CustomColumn\nRanttOutputTests.TestMethod,OverrideValue";
        await _fileSystem.WriteAllTextAsync(_fileSystem.Combine(_testFolderPath, "TestReporter", "method_overrides.csv"), overrideContent);

        var methodCallInfo = CreateMethodCallInfo("TestMethod", null);
        methodCallInfo.AddParameter("CustomColumn", "OriginalValue");
        methodCallInfo.AttributeParameters.Add("CustomColumn");

        await using (var _ = _ranttOutput.Initialize(_mockReporter))
        {
            _ranttOutput.WriteItem(new MethodCallStart(methodCallInfo));
            _ranttOutput.WriteItem(new MethodCallEnd(methodCallInfo));
        }

        // Assert
        var csvFilePath = _fileSystem.Combine(_testFolderPath, "TestReporter", "TestReporter.csv");
        var csvContent = await _fileSystem.ReadAllTextAsync(csvFilePath);

        var lines = csvContent.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        var headers = lines[0].Split(',');
        var customColumnIndex = Array.IndexOf(headers, "CustomColumn");

        var values = lines[1].Split(',');

        Assert.That(values[customColumnIndex], Is.EqualTo("OverrideValue"), "Override value should be used");
    }

    [Test]
    [Ignore("Not important at the moment")]
    public async Task Initialize_ShouldThrowUnauthorizedAccessException_WhenFolderIsReadOnly()
    {
        _logger.LogInformation("Starting Initialize_ShouldThrowUnauthorizedAccessException_WhenFolderIsReadOnly test");

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
        _logger.LogInformation("Starting WriteItem_ShouldHandleCorruptDataGracefully test");

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
        _logger.LogInformation("Starting ExportToRantt_ShouldHandleInvalidXmlCharactersGracefully test");

        var methodName = "Method<with>Invalid&Xml\"Chars";
        var methodCallInfo = CreateMethodCallInfo(methodName, null);

        await using (var _ = _ranttOutput.Initialize(_mockReporter))
        {
            _ranttOutput.WriteItem(new MethodCallStart(methodCallInfo));
            _ranttOutput.WriteItem(new MethodCallEnd(methodCallInfo));
        }

        var csvFilePath = _fileSystem.Combine(_testFolderPath, "TestReporter", "TestReporter.csv");
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

        var ranttFilePath = _fileSystem.Combine(_testFolderPath, "TestReporter", "TestReporter.rprjx");
        Assert.That(_fileSystem.FileExists(ranttFilePath), Is.True, "Rantt project file should be created");

        var ranttContent = await _fileSystem.ReadAllTextAsync(ranttFilePath);
        Assert.That(ranttContent, Does.Not.Contain("<with>"),
            "Unescaped XML content should not be present in Rantt project file");
        Assert.That(ranttContent, Does.Not.Contain("Invalid&Xml\"Chars"),
            "Unescaped XML content should not be present in Rantt project file");
    }

    [Test]
    public async Task RanttOutput_EnsuresCorrectRootMethodForReporterAndInvariants()
    {
        _logger.LogInformation("Starting RanttOutput_EnsuresCorrectRootMethodForReporterAndInvariants test");

        // Arrange
        var rootMethodInfo = CreateMethodCallInfo("RootMethod", null);
        var childMethodInfo = CreateMethodCallInfo("ChildMethod", rootMethodInfo);
        _mockReporter = new MockReporter(_loggerFactory)
        {
            Name = TestReporterName,
            FullName = TestReporterName
        };

        rootMethodInfo.AddAssociatedReporter(_mockReporter);

        _mockReporter.Initialize(new MonitoringConfiguration(), rootMethodInfo);

        // Act
        await using (var _ = _ranttOutput.Initialize(_mockReporter))
        {
            _ranttOutput.WriteItem(new MethodCallStart(rootMethodInfo));
            _ranttOutput.WriteItem(new MethodCallStart(childMethodInfo));
            _ranttOutput.WriteItem(new MethodCallEnd(childMethodInfo));
            _ranttOutput.WriteItem(new MethodCallEnd(rootMethodInfo));
        }

        // Assert
        var csvFilePath = GetFilePath(CsvFileName);
        AssertFileExists(csvFilePath);

        var csvContent = await _fileSystem.ReadAllTextAsync(csvFilePath);
        _logger.LogInformation($"CSV Content:\n{csvContent}");

        var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var headers = lines[0].Split(',');
        var dataLines = lines.Skip(1).ToList(); // Skip header

        Assert.That(dataLines, Has.Count.GreaterThan(0), "CSV should contain data lines");

        var idIndex = Array.IndexOf(headers, "Id");
        var parentIdIndex = Array.IndexOf(headers, "ParentId");
        Assert.That(idIndex, Is.GreaterThanOrEqualTo(0), "CSV should have an 'Id' column");
        Assert.That(parentIdIndex, Is.GreaterThanOrEqualTo(0), "CSV should have a 'ParentId' column");

        // Check invariant for all lines
        foreach (var line in dataLines)
        {
            var fields = line.Split(',');
            var id = fields[idIndex];
            var parentId = fields[parentIdIndex];

            Assert.That(id, Is.Not.EqualTo(parentId),
                $"Id should never equal ParentId. Violating line: {line}");
        }

        var rootLine = dataLines.FirstOrDefault(l => l.Contains("RootMethod"));
        Assert.That(rootLine, Is.Not.Null, "CSV should contain a line for the RootMethod");
        var rootFields = rootLine.Split(',');
        Assert.That(rootFields[idIndex], Is.EqualTo("ROOT"), "RootMethod should have 'ROOT' as its Id");
        Assert.That(rootFields[parentIdIndex], Is.Empty.Or.Null, "RootMethod should have an empty or null ParentId");

        var childLine = dataLines.FirstOrDefault(l => l.Contains("ChildMethod"));
        Assert.That(childLine, Is.Not.Null, "CSV should contain a line for the ChildMethod");
        var childFields = childLine.Split(',');
        Assert.That(childFields[idIndex], Is.Not.EqualTo("ROOT"), "ChildMethod should not have 'ROOT' as its Id");
        Assert.That(childFields[parentIdIndex], Is.EqualTo("ROOT"), "ChildMethod should have 'ROOT' as its ParentId");

        _logger.LogInformation($"Root Line: {rootLine}");
        _logger.LogInformation($"Child Line: {childLine}");
    }

    [Test]
    public async Task ExportToCsv_IncludesStaticAndDynamicParameters()
    {
        // Arrange
        var methodCallInfo = CreateMethodCallInfo("TestMethod", null);
        methodCallInfo.AddParameter("StaticParam", "StaticValue");
        methodCallInfo.AddParameter("DynamicParam", "DynamicValue");
        methodCallInfo.AttributeParameters.Add("StaticParam");

        await using (var _ = _ranttOutput.Initialize(_mockReporter))
        {
            _ranttOutput.WriteItem(new MethodCallStart(methodCallInfo));
            _ranttOutput.WriteItem(new MethodCallEnd(methodCallInfo));
        }

        // Assert
        var csvFilePath = _fileSystem.Combine(_testFolderPath, "TestReporter", "TestReporter.csv");
        var csvContent = await _fileSystem.ReadAllTextAsync(csvFilePath);
        var lines = csvContent.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        var headers = lines[0].Split(',');
        var staticParameterIndex = Array.IndexOf(headers, "StaticParam");
        var dynamicParameterIndex = Array.IndexOf(headers, "DynamicParam");

        Assert.That(dynamicParameterIndex, Is.EqualTo(-1));

        var dataLine = lines[1].Split(',');
        var staticParameterValue = dataLine[staticParameterIndex];

        Assert.That(staticParameterValue, Is.EqualTo("StaticValue"));
    }

    [Test]
    public async Task RanttOutput_ShouldPreserveParameterTypesWhenExporting()
    {
        // Arrange
        var methodCallInfo = CreateMethodCallInfo("TestMethod", null);
        methodCallInfo.AddParameter("StaticParam", "StaticValue");
        methodCallInfo.AddParameter("DynamicParam", "DynamicValue");
        methodCallInfo.AttributeParameters.Add("StaticParam");

        await using (var _ = _ranttOutput.Initialize(_mockReporter))
        {
            _ranttOutput.WriteItem(new MethodCallStart(methodCallInfo));
            _ranttOutput.WriteItem(new MethodCallEnd(methodCallInfo));
        }

        // Assert
        var csvFilePath = _fileSystem.Combine(_testFolderPath, "TestReporter", "TestReporter.csv");
        var csvContent = await _fileSystem.ReadAllTextAsync(csvFilePath);

        var lines = csvContent.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.That(lines[0], Does.Contain("StaticParam"));
        Assert.That(lines[0], Does.Not.Contain("DynamicParam"));
        Assert.That(lines[1], Does.Contain("StaticValue"));
        Assert.That(lines[1], Does.Not.Contain("DynamicValue"));
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
        return _fileSystem.Combine(_testFolderPath, TestReporterName, fileName);
    }

    private void AssertFileExists(string filePath)
    {
        Assert.That(_fileSystem.FileExists(filePath), Is.True, $"{filePath} should exist");
    }

    private string CreateReadOnlyTestFolder()
    {
        var readOnlyFolder = _fileSystem.Combine(_testFolderPath, "ReadOnly");
        _fileSystem.CreateDirectory(readOnlyFolder);
        _fileSystem.SetAttributes(readOnlyFolder, FileAttributes.ReadOnly);
        return readOnlyFolder;
    }
}

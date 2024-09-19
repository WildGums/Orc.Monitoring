#pragma warning disable CL0002
namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using Reporters.ReportOutputs;
using System.Collections.Generic;
using System.IO;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TestUtilities.Logging;
using TestUtilities.Mocks;

[TestFixture]
public class CsvReportWriterAdditionalTests
{
    private StringWriter _stringWriter;
    private MethodOverrideManager _overrideManager;
    private List<ReportItem> _reportItems;
    private TestLogger<CsvReportWriterAdditionalTests> _logger;
    private TestLoggerFactory<CsvReportWriterAdditionalTests> _loggerFactory;
    private InMemoryFileSystem _fileSystem;
    private CsvUtils _csvUtils;

    [SetUp]
    public void Setup()
    {
        _logger = new TestLogger<CsvReportWriterAdditionalTests>();
        _loggerFactory = new TestLoggerFactory<CsvReportWriterAdditionalTests>(_logger);
        _fileSystem = new InMemoryFileSystem(_loggerFactory);
        _csvUtils = new CsvUtils(_fileSystem);

        _stringWriter = new StringWriter();
        _overrideManager = new MethodOverrideManager(Path.GetTempPath(), _loggerFactory, _fileSystem, _csvUtils);
        _reportItems = [];
    }

    [TearDown]
    public void TearDown()
    {
        _fileSystem.Dispose();
        _stringWriter.Dispose();
    }

    [Test]
    public async Task WriteReportItemsCsvAsync_HandlesCustomColumns()
    {
        // Arrange
        _reportItems.Add(new ReportItem
        {
            Id = "1",
            MethodName = "TestMethod1",
            FullName = "TestClass.TestMethod1",
            StartTime = "2023-01-01 00:00:00.000",
            Parameters = new Dictionary<string, string>
            {
                {"CustomColumn1", "Value1"},
                {"CustomColumn2", "Value2"}
            },
            AttributeParameters = new HashSet<string> { "CustomColumn1" }
        });
        _reportItems.Add(new ReportItem
        {
            Id = "2",
            MethodName = "TestMethod2",
            FullName = "TestClass.TestMethod2",
            StartTime = "2023-01-01 00:00:01.000",
            Parameters = new Dictionary<string, string>
            {
                {"CustomColumn1", "Value3"},
                {"CustomColumn3", "Value4"}
            },
            AttributeParameters = new HashSet<string> { "CustomColumn1", "CustomColumn3" }
        });

        var writer = new CsvReportWriter(_stringWriter, _reportItems, _overrideManager);

        // Act
        await writer.WriteReportItemsCsvAsync();

        // Assert
        var content = _stringWriter.ToString();
        var lines = content.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.That(lines.Length, Is.EqualTo(3), "Should have header and two data lines");
        Assert.That(lines[0], Does.Contain("CustomColumn1"));
        Assert.That(lines[0], Does.Not.Contain("CustomColumn2"));
        Assert.That(lines[0], Does.Contain("CustomColumn3"));

        Assert.That(lines[1], Does.Contain("Value1"));
        Assert.That(lines[1], Does.Not.Contain("Value2"));
        Assert.That(lines[2], Does.Contain("Value3"));
        Assert.That(lines[2], Does.Contain("Value4"));
    }

    [Test]
    public async Task WriteRelationshipsCsvAsync_HandlesVariousRelationshipTypes()
    {
        // Arrange
        _reportItems.Add(new ReportItem { Id = "1", Parent = "0", MethodName = "ParentMethod", Parameters = new Dictionary<string, string> { { "IsStatic", "True" } } });
        _reportItems.Add(new ReportItem { Id = "2", Parent = "1", MethodName = "ChildMethod", Parameters = new Dictionary<string, string> { { "IsExtension", "True" } } });
        _reportItems.Add(new ReportItem { Id = "3", Parent = "1", MethodName = "GenericMethod", Parameters = new Dictionary<string, string> { { "IsGeneric", "True" } } });

        var writer = new CsvReportWriter(_stringWriter, _reportItems, _overrideManager);

        // Act
        await writer.WriteRelationshipsCsvAsync();

        // Assert
        var content = _stringWriter.ToString();
        var lines = content.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.That(lines[1], Does.Contain("Static"));
        Assert.That(lines[2], Does.Contain("Extension"));
        Assert.That(lines[3], Does.Contain("Generic"));
    }

    [Test]
    public async Task WriteReportItemsCsvAsync_HandlesEmptyReportItems()
    {
        // Arrange
        var writer = new CsvReportWriter(_stringWriter, _reportItems, _overrideManager);

        // Act
        await writer.WriteReportItemsCsvAsync();

        // Assert
        var content = _stringWriter.ToString();
        var lines = content.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.That(lines.Length, Is.EqualTo(1), "Should only have header line");
    }

    [Test]
    public async Task WriteReportItemsCsvAsync_HandlesNullValues()
    {
        // Arrange
        _reportItems.Add(new ReportItem
        {
            Id = null,
            MethodName = null,
            FullName = null,
            StartTime = "2023-01-01 00:00:00.000",
            Parameters = new Dictionary<string, string>
            {
                {"NullValueParam", null}
            }
        });

        var writer = new CsvReportWriter(_stringWriter, _reportItems, _overrideManager);

        // Act
        await writer.WriteReportItemsCsvAsync();

        // Assert
        var content = _stringWriter.ToString();
        _logger.LogInformation($"CSV Content:\n{content}");
        var lines = content.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.That(lines.Length, Is.EqualTo(2), "Should have header and one data line");
        Assert.That(lines[1], Does.Contain(",,"), "Should contain empty values for null fields");
        Assert.That(lines[1], Does.Contain("2023-01-01 00:00:00.000"), "Should contain the StartTime");
    }
}

#pragma warning disable CL0002
namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using Orc.Monitoring.Reporters.ReportOutputs;
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using Moq;

[TestFixture]
public class CsvReportWriterTests
{
    private StringWriter _stringWriter;
    private MethodOverrideManager _overrideManager;
    private List<ReportItem> _reportItems;
    private TestLogger<CsvReportWriterTests> _logger;
    private TestLoggerFactory<CsvReportWriterTests> _loggerFactory;
    private InMemoryFileSystem _fileSystem;
    private CsvUtils _csvUtils;

    [SetUp]
    public void Setup()
    {
        _logger = new TestLogger<CsvReportWriterTests>();
        _loggerFactory = new TestLoggerFactory<CsvReportWriterTests>(_logger);
        _fileSystem = new InMemoryFileSystem(_loggerFactory);
        _csvUtils = new CsvUtils(_fileSystem);

        _stringWriter = new StringWriter();
        _overrideManager = new MethodOverrideManager(Path.GetTempPath(), _loggerFactory, _fileSystem, _csvUtils);
        _reportItems = new List<ReportItem>();
    }

    [TearDown]
    public void TearDown()
    {
        _fileSystem.Dispose();
        _stringWriter.Dispose();
    }

    [Test]
    public async Task WriteReportItemsCsvAsync_WritesCorrectHeaders()
    {
        var writer = new CsvReportWriter(_stringWriter, _reportItems, _overrideManager);
        await writer.WriteReportItemsCsvAsync();

        var content = _stringWriter.ToString();
        var headers = content.Split(Environment.NewLine)[0].Split(',');

        Assert.That(headers, Does.Contain("Id"));
        Assert.That(headers, Does.Contain("ParentId"));
        Assert.That(headers, Does.Contain("StartTime"));
        Assert.That(headers, Does.Contain("EndTime"));
        Assert.That(headers, Does.Contain("MethodName"));
        Assert.That(headers, Does.Contain("Duration"));
    }

    [Test]
    public async Task WriteReportItemsCsvAsync_WritesReportItemsCorrectly()
    {
        _reportItems.Add(new ReportItem
        {
            Id = "1",
            StartTime = "2023-01-01 00:00:00.000",
            EndTime = "2023-01-01 00:00:01.000",
            MethodName = "TestMethod",
            Duration = "1000",
            Parameters = new Dictionary<string, string> { { "TestParam", "TestValue" } }
        });

        var writer = new CsvReportWriter(_stringWriter, _reportItems, _overrideManager);
        await writer.WriteReportItemsCsvAsync();

        var content = _stringWriter.ToString();
        var lines = content.Split(Environment.NewLine);

        Assert.That(lines.Length, Is.GreaterThan(1));
        Assert.That(lines[1], Does.Contain("1"));
        Assert.That(lines[1], Does.Contain("2023-01-01 00:00:00.000"));
        Assert.That(lines[1], Does.Contain("TestMethod"));
        Assert.That(lines[1], Does.Contain("1000"));
        Assert.That(lines[1], Does.Contain("TestValue"));
    }

    [Test]
    public async Task WriteReportItemsCsvAsync_AppliesOverrides()
    {
        var customColumnName = "CustomColumn";
        var customValue = "CustomValue";
        var overrideValue = "OverrideValue";
        _reportItems.Add(new ReportItem
        {
            Id = "1",
            MethodName = "TestMethod",
            FullName = "TestNamespace.TestClass.TestMethod",
            StartTime = "2023-01-01 00:00:00.000",
            Parameters = new Dictionary<string, string> { { customColumnName, customValue } }
        });

        // Setup override
        _overrideManager.ReadOverrides();
        var overrides = new Dictionary<string, string> { { customColumnName, overrideValue } };
        _overrideManager.GetType().GetField("_overrides", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .SetValue(_overrideManager, new Dictionary<string, Dictionary<string, string>> { { "TestNamespace.TestClass.TestMethod", overrides } });

        var writer = new CsvReportWriter(_stringWriter, _reportItems, _overrideManager);
        await writer.WriteReportItemsCsvAsync();

        var content = _stringWriter.ToString();
        _logger.LogInformation($"CSV Content:\n{content}");

        var lines = content.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        _logger.LogInformation($"Number of lines: {lines.Length}");

        Assert.That(lines.Length, Is.GreaterThanOrEqualTo(2), "CSV content should contain at least header and data line");

        var headers = lines[0].Split(',');
        var dataLine = lines[1].Split(',');

        _logger.LogInformation($"Headers: {string.Join(", ", headers)}");
        _logger.LogInformation($"Data line: {string.Join(", ", dataLine)}");

        Assert.That(headers, Does.Contain(customColumnName), "Headers should contain the custom column");

        var customColumnIndex = Array.IndexOf(headers, customColumnName);
        Assert.That(customColumnIndex, Is.GreaterThanOrEqualTo(0), $"Custom column {customColumnName} not found in headers");
        Assert.That(dataLine[customColumnIndex], Is.EqualTo(overrideValue), "Custom column value should be the override value");
    }

    [Test]
    public async Task WriteReportItemsCsvAsync_HandlesMultipleItems()
    {
        _reportItems.AddRange(new[]
        {
            new ReportItem
            {
                Id = "1",
                StartTime = "2023-01-01 00:00:02.000",
                EndTime = "2023-01-01 00:00:03.000",
                MethodName = "Method1",
                ThreadId = "1",
                Level = "1",
                Parameters = new Dictionary<string, string> { { "Param1", "Value1" } }
            },
            new ReportItem
            {
                Id = "2",
                StartTime = "2023-01-01 00:00:01.000",
                EndTime = "2023-01-01 00:00:04.000",
                MethodName = "Method2",
                ThreadId = "2",
                Level = "1",
                Parameters = new Dictionary<string, string> { { "Param2", "Value2" } }
            },
            new ReportItem
            {
                Id = "3",
                StartTime = "2023-01-01 00:00:01.500",
                EndTime = "2023-01-01 00:00:02.500",
                MethodName = "Method3",
                ThreadId = "1",
                Level = "2",
                Parameters = new Dictionary<string, string> { { "Param3", "Value3" } }
            }
        });

        var writer = new CsvReportWriter(_stringWriter, _reportItems, _overrideManager);
        await writer.WriteReportItemsCsvAsync();

        var content = _stringWriter.ToString();
        _logger.LogInformation($"CSV Content:\n{content}");
        var lines = content.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.That(lines.Length, Is.EqualTo(4), "Should have header and 3 data lines");

        var dataLines = lines.Skip(1).ToArray();
        Assert.That(dataLines[0], Does.Contain("Method2"), "First line should be Method2 (earliest start time)");
        Assert.That(dataLines[1], Does.Contain("Method3"), "Second line should be Method3 (second earliest start time)");
        Assert.That(dataLines[2], Does.Contain("Method1"), "Third line should be Method1 (latest start time)");

        Assert.That(dataLines[0], Does.Contain("Value2"), "Should contain Parameter value for Method2");
        Assert.That(dataLines[1], Does.Contain("Value3"), "Should contain Parameter value for Method3");
        Assert.That(dataLines[2], Does.Contain("Value1"), "Should contain Parameter value for Method1");
    }

    [Test]
    public async Task WriteReportItemsCsvAsync_HandlesNullValues()
    {
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
        await writer.WriteReportItemsCsvAsync();

        var content = _stringWriter.ToString();
        _logger.LogInformation($"CSV Content:\n{content}");
        var lines = content.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.That(lines.Length, Is.EqualTo(2), "Should have header and one data line");
        Assert.That(lines[1], Does.Contain(",,"), "Should contain empty values for null fields");
        Assert.That(lines[1], Does.Contain("2023-01-01 00:00:00.000"), "Should contain the StartTime");
    }

    [Test]
    public async Task WriteReportItemsCsvAsync_HandlesEmptyReportItems()
    {
        var writer = new CsvReportWriter(_stringWriter, _reportItems, _overrideManager);
        await writer.WriteReportItemsCsvAsync();

        var content = _stringWriter.ToString();
        var lines = content.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.That(lines.Length, Is.EqualTo(1), "Should only have header line");
    }

    [Test]
    public async Task WriteReportItemsCsvAsync_DoesNotAddEmptyLineAtEnd()
    {
        // Arrange
#pragma warning disable IDISP001
        var stringWriter = new StringWriter();
#pragma warning restore IDISP001
        var reportItems = new List<ReportItem>
        {
            new ReportItem { Id = "1", MethodName = "Method1", StartTime = "2023-01-01 00:00:00" },
            new ReportItem { Id = "2", MethodName = "Method2", StartTime = "2023-01-01 00:00:01" }
        };
        var overrideManager = new Mock<MethodOverrideManager>(Path.GetTempPath(), _loggerFactory, _fileSystem, _csvUtils).Object;
        var writer = new CsvReportWriter(stringWriter, reportItems, overrideManager, _loggerFactory, _csvUtils);

        // Act
        await writer.WriteReportItemsCsvAsync();

        // Assert
        var content = stringWriter.ToString();
        _logger.LogInformation($"CSV Content:\n{content}");

        var lines = content.Split('\n');
        Assert.That(lines.Length, Is.EqualTo(3), "Should have exactly 3 lines (header + 2 data lines)");
        Assert.That(lines[2], Does.Not.EndWith("\n"), "Last line should not end with a newline");
        Assert.That(content, Does.Not.EndWith("\n"), "CSV content should not end with a newline");
    }
}

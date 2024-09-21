#pragma warning disable CL0002
namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using Reporters.ReportOutputs;
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using Core.Models;
using Core.Utilities;
using Moq;
using TestUtilities.Logging;
using TestUtilities.Mocks;
using Orc.Monitoring.TestUtilities;

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
    private string _overrideFilePath;

    [SetUp]
    public void Setup()
    {
        _logger = new TestLogger<CsvReportWriterTests>();
        _loggerFactory = new TestLoggerFactory<CsvReportWriterTests>(_logger);
        _loggerFactory.EnableLoggingFor<CsvReportWriter>();
        _loggerFactory.EnableLoggingFor<MethodOverrideManager>();
        _fileSystem = new InMemoryFileSystem(_loggerFactory);
        _csvUtils = TestHelperMethods.CreateCsvUtils(_fileSystem, _loggerFactory);

        _stringWriter = new StringWriter();
        _overrideFilePath = _fileSystem.GetTempPath();
        _overrideManager = new MethodOverrideManager(_overrideFilePath, _loggerFactory, _fileSystem, _csvUtils);
        _reportItems = [];
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
        // Arrange
        _reportItems.Add(new ReportItem
        {
            Id = "1",
            MethodName = "TestMethod",
            FullName = "TestClass.TestMethod",
            StartTime = "2023-01-01 00:00:00.000",
            EndTime = "2023-01-01 00:00:01.000",
            Duration = "1000",
            Parameters = new Dictionary<string, string>
            {
                { "StaticParam", "StaticValue" },
                { "DynamicParam", "DynamicValue" }
            },
            AttributeParameters = new HashSet<string> { "StaticParam" }
        });

        var writer = new CsvReportWriter(_stringWriter, _reportItems, _overrideManager);

        // Act
        await writer.WriteReportItemsCsvAsync();

        // Assert
        var content = _stringWriter.ToString();
        var lines = content.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.That(lines.Length, Is.EqualTo(2), "Should have header and one data line");
        Assert.That(lines[1], Does.Contain("1"));
        Assert.That(lines[1], Does.Contain("TestMethod"));
        Assert.That(lines[1], Does.Contain("TestClass.TestMethod"));
        Assert.That(lines[1], Does.Contain("2023-01-01 00:00:00.000"));
        Assert.That(lines[1], Does.Contain("2023-01-01 00:00:01.000"));
        Assert.That(lines[1], Does.Contain("1000"));
        Assert.That(lines[1], Does.Contain("StaticValue"));
        Assert.That(lines[1], Does.Contain("DynamicValue"));
    }

    [Test]
    public async Task WriteReportItemsCsvAsync_AppliesOverrides()
    {
        // Arrange
        var overrideContent = "FullName,CustomColumn\nTestClass.Method1,OverrideValue";
        var overrideFilePath = _fileSystem.Combine(_overrideFilePath, "method_overrides.csv");
        await _fileSystem.WriteAllTextAsync(overrideFilePath, overrideContent);
        _overrideManager.ReadOverrides();

        _reportItems.Add(new ReportItem
        {
            Id = "1",
            MethodName = "Method1",
            FullName = "TestClass.Method1",
            StartTime = "2023-01-01 00:00:00.000",
            Parameters = new Dictionary<string, string> { { "CustomColumn", "OriginalValue" } },
            AttributeParameters = new HashSet<string> { "CustomColumn" }
        });

        var writer = new CsvReportWriter(_stringWriter, _reportItems, _overrideManager, _loggerFactory, _csvUtils);

        // Act
        await writer.WriteReportItemsCsvAsync();

        // Assert
        var content = _stringWriter.ToString();
        _logger.LogInformation($"CSV Content:\n{content}");
        var lines = content.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.That(lines.Length, Is.EqualTo(2), "Should have header and one data line");
        Assert.That(lines[1], Does.Contain("OverrideValue"));
        Assert.That(lines[1], Does.Not.Contain("OriginalValue"));
    }

    [Test]
    public async Task WriteReportItemsCsvAsync_HandlesMultipleItems()
    {
        // Arrange
        _reportItems.AddRange(new[]
        {
            new ReportItem
            {
                Id = "1",
                MethodName = "Method1",
                FullName = "TestClass.Method1",
                StartTime = "2023-01-01 00:00:01.000",
                Parameters = new Dictionary<string, string> { { "Param1", "Value1" } },
                AttributeParameters = new HashSet<string> { "Param1" }
            },
            new ReportItem
            {
                Id = "2",
                MethodName = "Method2",
                FullName = "TestClass.Method2",
                StartTime = "2023-01-01 00:00:02.000",
                Parameters = new Dictionary<string, string> { { "Param2", "Value2" } },
                AttributeParameters = new HashSet<string>()
            }
        });

        var writer = new CsvReportWriter(_stringWriter, _reportItems, _overrideManager);

        // Act
        await writer.WriteReportItemsCsvAsync();

        // Assert
        var content = _stringWriter.ToString();
        var lines = content.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.That(lines.Length, Is.EqualTo(3), "Should have header and two data lines");
        Assert.That(lines[0], Does.Contain("Param1"));
        Assert.That(lines[0], Does.Contain("Param2"));
        Assert.That(lines[1], Does.Contain("Value1"));
        Assert.That(lines[2], Does.Contain("Value2"));
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
            new() { Id = "1", MethodName = "Method1", StartTime = "2023-01-01 00:00:00" },
            new() { Id = "2", MethodName = "Method2", StartTime = "2023-01-01 00:00:01" }
        };
        var overrideManager = new Mock<MethodOverrideManager>(_fileSystem.GetTempPath(), _loggerFactory, _fileSystem, _csvUtils).Object;
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

    [Test]
    public async Task WriteReportItemsCsvAsync_DistinguishesStaticAndDynamicParameters()
    {
        // Arrange
        _reportItems.Add(new ReportItem
        {
            Id = "1",
            MethodName = "TestMethod",
            FullName = "TestClass.TestMethod",
            StartTime = "2023-01-01 00:00:00.000",
            Parameters = new Dictionary<string, string>
            {
                { "StaticParam", "StaticValue" },
                { "DynamicParam", "DynamicValue" }
            },
            AttributeParameters = new HashSet<string> { "StaticParam" }
        });

        var writer = new CsvReportWriter(_stringWriter, _reportItems, _overrideManager);

        // Act
        await writer.WriteReportItemsCsvAsync();

        // Assert
        var content = _stringWriter.ToString();
        var lines = content.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.That(lines.Length, Is.EqualTo(2), "Should have header and one data line");
        Assert.That(lines[0], Does.Contain("StaticParam"));
        Assert.That(lines[0], Does.Contain("DynamicParam"));
        Assert.That(lines[1], Does.Contain("StaticValue"));
        Assert.That(lines[1], Does.Contain("DynamicValue"));
    }

    [Test]
    public async Task CsvReportWriter_ShouldCorrectlyLabelStaticAndDynamicParameters()
    {
        // Arrange
        _reportItems.Add(new ReportItem
        {
            Id = "1",
            MethodName = "TestMethod",
            FullName = "TestClass.TestMethod",
            Parameters = new Dictionary<string, string>
            {
                { "StaticParam1", "StaticValue1" },
                { "StaticParam2", "StaticValue2" },
                { "DynamicParam1", "DynamicValue1" },
                { "DynamicParam2", "DynamicValue2" }
            },
            AttributeParameters = new HashSet<string> { "StaticParam1", "StaticParam2" }
        });

        var writer = new CsvReportWriter(_stringWriter, _reportItems, _overrideManager);

        // Act
        await writer.WriteReportItemsCsvAsync();

        // Assert
        var content = _stringWriter.ToString();
        var lines = content.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.That(lines[0], Does.Contain("StaticParam1"));
        Assert.That(lines[0], Does.Contain("StaticParam2"));
        Assert.That(lines[0], Does.Contain("DynamicParam1"));
        Assert.That(lines[0], Does.Contain("DynamicParam2"));

        Assert.That(lines[1], Does.Contain("StaticValue1"));
        Assert.That(lines[1], Does.Contain("StaticValue2"));
        Assert.That(lines[1], Does.Contain("DynamicValue1"));
        Assert.That(lines[1], Does.Contain("DynamicValue2"));
    }

    [Test]
    public async Task WriteReportItemsCsvAsync_ShouldOnlyApplyOverridesToStaticParameters()
    {
        // Arrange
        var overrideContent = "FullName,StaticParam\nTestClass.TestMethod,StaticOverride";
        await _fileSystem.WriteAllTextAsync(_fileSystem.Combine(_fileSystem.GetTempPath(), "method_overrides.csv"), overrideContent);

        _reportItems.Add(new ReportItem
        {
            Id = "1",
            MethodName = "TestMethod",
            FullName = "TestClass.TestMethod",
            Parameters = new Dictionary<string, string>
            {
                {"StaticParam", "OriginalStatic"},
                {"DynamicParam", "OriginalDynamic"}
            },
            AttributeParameters = new HashSet<string> { "StaticParam" }
        });

        var overrideManager = new MethodOverrideManager(_fileSystem.GetTempPath(), _loggerFactory, _fileSystem, _csvUtils);
        overrideManager.ReadOverrides();

        var writer = new CsvReportWriter(_stringWriter, _reportItems, overrideManager, _loggerFactory, _csvUtils);

        // Act
        await writer.WriteReportItemsCsvAsync();

        // Assert
        var content = _stringWriter.ToString();
        var lines = content.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.That(lines[0], Does.Contain("StaticParam"));
        Assert.That(lines[0], Does.Contain("DynamicParam"));
        Assert.That(lines[1], Does.Contain("StaticOverride"));
        Assert.That(lines[1], Does.Not.Contain("DynamicOverride"));
        Assert.That(lines[1], Does.Contain("OriginalDynamic"));
    }
}

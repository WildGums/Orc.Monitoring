#pragma warning disable CL0002
namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using Orc.Monitoring.Reporters.ReportOutputs;
using System.Collections.Generic;
using System.IO;
using System;
using System.Threading.Tasks;

[TestFixture]
public class CsvReportWriterAdditionalTests
{
    private StringWriter _stringWriter;
    private MethodOverrideManager _overrideManager;
    private List<ReportItem> _reportItems;

    [SetUp]
    public void Setup()
    {
        _stringWriter = new StringWriter();
        _overrideManager = new MethodOverrideManager(Path.GetTempPath());
        _reportItems = new List<ReportItem>();
    }

    [TearDown]
    public void TearDown()
    {
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
            AttributeParameters = new HashSet<string> { "CustomColumn1", "CustomColumn2" }
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

        _overrideManager.SaveOverrides(_reportItems);
        var writer = new CsvReportWriter(_stringWriter, _reportItems, _overrideManager);

        // Act
        await writer.WriteReportItemsCsvAsync();

        // Assert
        var content = _stringWriter.ToString();
        Console.WriteLine($"CSV Content:\n{content}");
        var lines = content.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Console.WriteLine($"Number of lines: {lines.Length}");

        Assert.That(lines.Length, Is.GreaterThanOrEqualTo(3), "Should have at least header and two data lines");

        if (lines.Length >= 3)
        {
            Assert.That(lines[0], Does.Contain("CustomColumn1"));
            Assert.That(lines[0], Does.Contain("CustomColumn2"));
            Assert.That(lines[0], Does.Contain("CustomColumn3"));

            var dataLine1 = lines[1].Split(',');
            var dataLine2 = lines[2].Split(',');

            Console.WriteLine($"Data line 1: {lines[1]}");
            Console.WriteLine($"Data line 2: {lines[2]}");

            Assert.That(dataLine1, Does.Contain("Value1"));
            Assert.That(dataLine1, Does.Contain("Value2"));
            Assert.That(dataLine2, Does.Contain("Value3"));
            Assert.That(dataLine2, Does.Contain("Value4"));
        }
        else
        {
            Assert.Fail("Not enough lines in the CSV output");
        }
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
        Console.WriteLine($"CSV Content:\n{content}");
        var lines = content.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.That(lines.Length, Is.EqualTo(2), "Should have header and one data line");
        Assert.That(lines[1], Does.Contain(",,"), "Should contain empty values for null fields");
        Assert.That(lines[1], Does.Contain("2023-01-01 00:00:00.000"), "Should contain the StartTime");
    }
}

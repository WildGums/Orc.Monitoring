﻿#pragma warning disable CL0002
namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using Orc.Monitoring.Reporters.ReportOutputs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

[TestFixture]
public class CsvReportWriterTests
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
    public async Task WriteReportItemsCsvAsync_HandlesCustomColumns()
    {
        var customColumnName = "CustomColumn";
        var customValue = "CustomValue";
        _reportItems.Add(new ReportItem
        {
            Id = "1",
            MethodName = "TestMethod",
            FullName = "TestMethod",
            StartTime = "2023-01-01 00:00:00.000",
            Parameters = new Dictionary<string, string> { { customColumnName, customValue } },
            AttributeParameters = new HashSet<string> { customColumnName }
        });

        // Save overrides to add the custom column
        _overrideManager.SaveOverrides(_reportItems);

        var writer = new CsvReportWriter(_stringWriter, _reportItems, _overrideManager);
        await writer.WriteReportItemsCsvAsync();

        var content = _stringWriter.ToString();
        Console.WriteLine($"CSV Content:\n{content}");

        var lines = content.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Console.WriteLine($"Number of lines: {lines.Length}");

        Assert.That(lines.Length, Is.GreaterThanOrEqualTo(2), "CSV content should contain at least header and data line");

        var headers = lines[0].Split(',');
        var dataLine = lines[1].Split(',');

        Console.WriteLine($"Headers: {string.Join(", ", headers)}");
        Console.WriteLine($"Data line: {string.Join(", ", dataLine)}");

        var customColumns = _overrideManager.GetCustomColumns();
        Console.WriteLine($"Custom columns: {string.Join(", ", customColumns)}");

        Assert.That(customColumns, Does.Contain(customColumnName), "Custom column should be in the list of custom columns");
        Assert.That(headers, Does.Contain(customColumnName), "Headers should contain the custom column");

        var customColumnIndex = Array.IndexOf(headers, customColumnName);
        Assert.That(customColumnIndex, Is.GreaterThanOrEqualTo(0), $"Custom column {customColumnName} not found in headers");
        Assert.That(dataLine[customColumnIndex], Is.EqualTo(customValue), "Custom column value should be in the data line");
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
        Console.WriteLine($"CSV Content:\n{content}");
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
    public async Task WriteRelationshipsCsvAsync_WritesCorrectRelationships()
    {
        _reportItems.Add(new ReportItem { Id = "1", Parent = "0", MethodName = "ParentMethod" });
        _reportItems.Add(new ReportItem { Id = "2", Parent = "1", MethodName = "ChildMethod" });

        var writer = new CsvReportWriter(_stringWriter, _reportItems, _overrideManager);
        await writer.WriteRelationshipsCsvAsync();

        var content = _stringWriter.ToString();
        var lines = content.Split(Environment.NewLine);

        Assert.That(lines.Length, Is.GreaterThan(2));
        Assert.That(lines[0], Is.EqualTo("From,To,RelationType"));
        Assert.That(lines[1], Does.StartWith("0,1,"));
        Assert.That(lines[2], Does.StartWith("1,2,"));
    }

    [Test]
    public void MethodOverrideManager_HandlesOverrides()
    {
        var fullName = "TestMethod";
        var customColumnName = "CustomColumn";
        var customValue = "CustomValue";

        // Add a report item with a custom column
        _reportItems.Add(new ReportItem
        {
            FullName = fullName,
            Parameters = new Dictionary<string, string> { { customColumnName, customValue } },
            AttributeParameters = new HashSet<string> { customColumnName }
        });

        // Save overrides
        _overrideManager.SaveOverrides(_reportItems);

        // Get overrides for the method
        var overrides = _overrideManager.GetOverridesForMethod(fullName);

        Console.WriteLine($"Overrides for {fullName}: {string.Join(", ", overrides.Select(kv => $"{kv.Key}={kv.Value}"))}");

        // Check if the custom column is in the list of custom columns
        var customColumns = _overrideManager.GetCustomColumns();
        Console.WriteLine($"Custom columns: {string.Join(", ", customColumns)}");

        Assert.That(customColumns, Does.Contain(customColumnName), "Custom column should be in the list of custom columns");
        Assert.That(overrides, Does.ContainKey(customColumnName), "Overrides should contain the custom column");
        Assert.That(overrides[customColumnName], Is.EqualTo(customValue), "Custom column value should match");
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
        Console.WriteLine($"CSV Content:\n{content}");
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
}

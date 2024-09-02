namespace Orc.Monitoring.Tests;

using System;
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;

[TestFixture]
public class CsvUtilsTests
{
    private string _testFilePath;
    private InMemoryFileSystem _fileSystem;
    private CsvUtils _csvUtils;
    private TestLogger<CsvUtilsTests> _logger;

    [SetUp]
    public void Setup()
    {
        _logger = new TestLogger<CsvUtilsTests>();
        _fileSystem = new InMemoryFileSystem();
        _csvUtils = new CsvUtils(_fileSystem);
        _testFilePath = Path.GetTempFileName();
    }

    [TearDown]
    public void TearDown()
    {
        _fileSystem.Dispose();
        if (_fileSystem.FileExists(_testFilePath))
        {
            _fileSystem.DeleteFile(_testFilePath);
        }
    }

    [Test]
    public void WriteCsvLine_WritesCorrectly()
    {
        using (var writer = new StringWriter())
        {
            _csvUtils.WriteCsvLine(writer, new[] { "Header1", "Header2", "Header3" });
            var result = writer.ToString().Trim();
            Assert.That(result, Is.EqualTo("Header1,Header2,Header3"));
        }
    }

    [Test]
    public void WriteCsvLine_HandlesSpecialCharacters()
    {
        using (var writer = new StringWriter())
        {
            _csvUtils.WriteCsvLine(writer, new[] { "Normal", "With,Comma", "With\"Quote" });
            var result = writer.ToString().Trim();
            Assert.That(result, Is.EqualTo("Normal,\"With,Comma\",\"With\"\"Quote\""));
        }
    }

    [Test]
    public void ReadCsv_ReadsCorrectly()
    {
        var testData = "Header1,Header2,Header3\nValue1,Value2,Value3\nValue4,Value5,Value6";
        _fileSystem.WriteAllText(_testFilePath, testData);

        var result = _csvUtils.ReadCsv(_testFilePath);

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result[0]["Header1"], Is.EqualTo("Value1"));
        Assert.That(result[1]["Header3"], Is.EqualTo("Value6"));
    }

    [Test]
    public void ReadCsv_HandlesQuotedValues()
    {
        var testData = "Header1,Header2,Header3\nValue1,\"Value,2\",Value3\nValue4,Value5,\"Value,6\"";
        _fileSystem.WriteAllText(_testFilePath, testData);

        var result = _csvUtils.ReadCsv(_testFilePath);

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result[0]["Header2"], Is.EqualTo("Value,2"));
        Assert.That(result[1]["Header3"], Is.EqualTo("Value,6"));
    }

    [Test]
    public void WriteCsv_WritesCorrectly()
    {
        var testData = new List<Dictionary<string, string>>
        {
            new Dictionary<string, string> { { "Header1", "Value1" }, { "Header2", "Value2" } },
            new Dictionary<string, string> { { "Header1", "Value3" }, { "Header2", "Value4" } }
        };

        _csvUtils.WriteCsv(_testFilePath, testData, new[] { "Header1", "Header2" });

        var lines = _fileSystem.ReadAllLines(_testFilePath);

        // Log the actual content for debugging
        _logger.LogInformation($"Actual CSV content ({lines.Length} lines):");
        foreach (var line in lines)
        {
            _logger.LogInformation(line);
        }

        Assert.That(lines.Length, Is.GreaterThanOrEqualTo(3), "Should have at least header and two data lines");
        Assert.That(lines[0], Is.EqualTo("Header1,Header2"), "First line should be the header");
        Assert.That(lines[1], Is.EqualTo("Value1,Value2"), "Second line should contain first row of data");
        Assert.That(lines[2], Is.EqualTo("Value3,Value4"), "Third line should contain second row of data");

        // If there's an extra line, ensure it's empty
        if (lines.Length > 3)
        {
            Assert.That(lines[3], Is.Empty, "If there's an extra line, it should be empty");
        }
    }
}

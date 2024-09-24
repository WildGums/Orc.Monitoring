namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using Orc.Monitoring.TestUtilities.Logging;
using Orc.Monitoring.TestUtilities.Mocks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TestUtilities;

[TestFixture]
public class CsvUtilsTests
{
    private string _testFilePath;
    private InMemoryFileSystem _fileSystem;
    private CsvUtils _csvUtils;
    private TestLogger<CsvUtils> _logger;

    [SetUp]
    public void Setup()
    {
        _logger = new TestLogger<CsvUtils>();
        var loggerFactory = new TestLoggerFactory<CsvUtils>(_logger);
        loggerFactory.EnableLoggingFor<CsvUtils>();
        _fileSystem = new InMemoryFileSystem(loggerFactory);
        _csvUtils = TestHelperMethods.CreateCsvUtils(_fileSystem, loggerFactory);
        _testFilePath = _fileSystem.GetTempFileName();
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
        using var writer = new StringWriter();
        _csvUtils.WriteCsvLine(writer, new[] { "Header1", "Header2", "Header3" });
        var result = writer.ToString().Trim();
        Assert.That(result, Is.EqualTo("Header1,Header2,Header3"));
    }

    [Test]
    public void WriteCsvLine_HandlesSpecialCharacters()
    {
        using var writer = new StringWriter();
        _csvUtils.WriteCsvLine(writer, new[] { "Normal", "With,Comma", "With\"Quote" });
        var result = writer.ToString().Trim();
        Assert.That(result, Is.EqualTo("Normal,\"With,Comma\",\"With\"\"Quote\""));
    }

    [Test]
    public void WriteCsvLine_HandlesLeadingTrailingSpaces()
    {
        using var writer = new StringWriter();
        _csvUtils.WriteCsvLine(writer, new[] { " Leading", "Trailing " });
        var result = writer.ToString().Trim();
        Assert.That(result, Is.EqualTo("\" Leading\",\"Trailing \""));
    }

    [Test]
    public void WriteCsvLine_HandlesEmptyFields()
    {
        using var writer = new StringWriter();
        _csvUtils.WriteCsvLine(writer, new[] { string.Empty, "Value", null });
        var result = writer.ToString().Trim();
        Assert.That(result, Is.EqualTo(",Value,"));
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
        var testData = "Header1,Header2,Header3\nValue1,\"Value,2\",Value3\nValue4,Value5,\"Value\"\"6\"\"\"";
        _fileSystem.WriteAllText(_testFilePath, testData);

        var result = _csvUtils.ReadCsv(_testFilePath);

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result[0]["Header2"], Is.EqualTo("Value,2"));
        Assert.That(result[1]["Header3"], Is.EqualTo("Value\"6\""));
    }

    [Test]
    public void ReadCsv_HandlesLeadingTrailingSpaces()
    {
        var testData = "\" Leading\",\"Trailing \"\n\" Value1 \",\" Value2 \"";
        _fileSystem.WriteAllText(_testFilePath, testData);

        var result = _csvUtils.ReadCsv(_testFilePath);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0][" Leading"], Is.EqualTo(" Value1 "));
        Assert.That(result[0]["Trailing "], Is.EqualTo(" Value2 "));
    }

    [Test]
    public void ReadCsv_HandlesEmptyFields()
    {
        var testData = "Header1,Header2,Header3\n,,\nValue1,,Value3";
        _fileSystem.WriteAllText(_testFilePath, testData);

        var result = _csvUtils.ReadCsv(_testFilePath);

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result[0]["Header1"], Is.EqualTo(string.Empty));
        Assert.That(result[0]["Header2"], Is.EqualTo(string.Empty));
        Assert.That(result[1]["Header1"], Is.EqualTo("Value1"));
        Assert.That(result[1]["Header3"], Is.EqualTo("Value3"));
    }

    [Test]
    public void ReadCsv_HandlesFieldsWithOnlyQuotes()
    {
        var testData = "Header1,Header2\n\"\"\"\",\"\"\"\"\"\"\nValue1,Value2";
        _fileSystem.WriteAllText(_testFilePath, testData);

        var result = _csvUtils.ReadCsv(_testFilePath);

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result[0]["Header1"], Is.EqualTo("\""));
        Assert.That(result[0]["Header2"], Is.EqualTo("\"\""));
    }

    [Test]
    public void ReadCsv_ThrowsExceptionOnMalformedLine()
    {
        var testData = "Header1,Header2\n\"Unclosed quote,Value2";
        _fileSystem.WriteAllText(_testFilePath, testData);

        Assert.Throws<FormatException>(() => _csvUtils.ReadCsv(_testFilePath));
    }

    [Test]
    public void ReadCsv_HandlesDifferentLineEndings()
    {
        var testData = "Header1,Header2\r\nValue1,Value2\r\nValue3,Value4";
        _fileSystem.WriteAllText(_testFilePath, testData);

        var result = _csvUtils.ReadCsv(_testFilePath);

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result[0]["Header1"], Is.EqualTo("Value1"));
        Assert.That(result[1]["Header2"], Is.EqualTo("Value4"));
    }

    [Test]
    public async Task WriteCsv_WritesCorrectlyAsync()
    {
        var testData = new List<Dictionary<string, string>> { new() { { "Header1", "Value1" }, { "Header2", "Value2" } }, new() { { "Header1", "Value3" }, { "Header2", "Value4" } } };

        await _csvUtils.WriteCsvAsync(_testFilePath, testData, new[] { "Header1", "Header2" });

        var lines = await _fileSystem.ReadAllLinesAsync(_testFilePath);

        _logger.LogInformation($"Actual CSV content ({lines.Length} lines):");
        foreach (var line in lines)
        {
            _logger.LogInformation(line);
        }

        Assert.That(lines.Length, Is.GreaterThanOrEqualTo(2), "Should have at least header and data lines");
        Assert.That(lines[0], Is.EqualTo("Header1,Header2"), "First line should be the header");
        Assert.That(lines[1], Is.EqualTo("Value1,Value2"), "Second line should contain first row of data");
        Assert.That(lines[2], Is.EqualTo("Value3,Value4"), "Third line should contain second row of data");
    }

    [Test]
    public void EscapeCsvValue_HandlesWhitespace()
    {
        var value = " Value ";
        var escapedValue = _csvUtils.EscapeCsvValue(value);
        Assert.That(escapedValue, Is.EqualTo("\" Value \""));
    }

    [Test]
    public void EscapeCsvValue_HandlesNewlines()
    {
        var value = "Value\nNewLine";
        var escapedValue = _csvUtils.EscapeCsvValue(value);
        Assert.That(escapedValue, Is.EqualTo("\"Value\nNewLine\""));
    }

    [Test]
    public void ParseCsvLine_ThrowsExceptionOnMalformedLine()
    {
        var line = "\"Unclosed quote,Value2";
        Assert.Throws<FormatException>(() => _csvUtils.ParseCsvLine(line));
    }

    [Test]
    public void ParseCsvLine_HandlesEmptyString()
    {
        var line = string.Empty;
        var result = _csvUtils.ParseCsvLine(line);
        Assert.That(result.Length, Is.EqualTo(1));
        Assert.That(result[0], Is.EqualTo(string.Empty));
    }

    [Test]
    public void EscapeCsvValue_HandlesVariousCases()
    {
        Assert.That(_csvUtils.EscapeCsvValue("Value"), Is.EqualTo("Value"));
        Assert.That(_csvUtils.EscapeCsvValue("Value,With,Commas"), Is.EqualTo("\"Value,With,Commas\""));
        Assert.That(_csvUtils.EscapeCsvValue("Value\"With\"Quotes"), Is.EqualTo("\"Value\"\"With\"\"Quotes\""));
        Assert.That(_csvUtils.EscapeCsvValue(" ValueWithSpaces "), Is.EqualTo("\" ValueWithSpaces \""));
    }
}


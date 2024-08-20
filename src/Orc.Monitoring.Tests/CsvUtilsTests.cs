namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using System.IO;
using System.Collections.Generic;
using System.Linq;


[TestFixture]
public class CsvUtilsTests
{
    private string _testFilePath;

    [SetUp]
    public void Setup()
    {
        _testFilePath = Path.GetTempFileName();
    }

    [TearDown]
    public void TearDown()
    {
        if (File.Exists(_testFilePath))
        {
            File.Delete(_testFilePath);
        }
    }

    [Test]
    public void WriteCsvLine_WritesCorrectly()
    {
        using (var writer = new StringWriter())
        {
            CsvUtils.WriteCsvLine(writer, new[] { "Header1", "Header2", "Header3" });
            var result = writer.ToString().Trim();
            Assert.That(result, Is.EqualTo("Header1,Header2,Header3"));
        }
    }

    [Test]
    public void WriteCsvLine_HandlesSpecialCharacters()
    {
        using (var writer = new StringWriter())
        {
            CsvUtils.WriteCsvLine(writer, new[] { "Normal", "With,Comma", "With\"Quote" });
            var result = writer.ToString().Trim();
            Assert.That(result, Is.EqualTo("Normal,\"With,Comma\",\"With\"\"Quote\""));
        }
    }

    [Test]
    public void ReadCsv_ReadsCorrectly()
    {
        var testData = "Header1,Header2,Header3\nValue1,Value2,Value3\nValue4,Value5,Value6";
        File.WriteAllText(_testFilePath, testData);

        var result = CsvUtils.ReadCsv(_testFilePath);

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result[0]["Header1"], Is.EqualTo("Value1"));
        Assert.That(result[1]["Header3"], Is.EqualTo("Value6"));
    }

    [Test]
    public void ReadCsv_HandlesQuotedValues()
    {
        var testData = "Header1,Header2,Header3\nValue1,\"Value,2\",Value3\nValue4,Value5,\"Value,6\"";
        File.WriteAllText(_testFilePath, testData);

        var result = CsvUtils.ReadCsv(_testFilePath);

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

        CsvUtils.WriteCsv(_testFilePath, testData, new[] { "Header1", "Header2" });

        var lines = File.ReadAllLines(_testFilePath);
        Assert.That(lines, Has.Length.EqualTo(3));
        Assert.That(lines[0], Is.EqualTo("Header1,Header2"));
        Assert.That(lines[1], Is.EqualTo("Value1,Value2"));
        Assert.That(lines[2], Is.EqualTo("Value3,Value4"));
    }
}

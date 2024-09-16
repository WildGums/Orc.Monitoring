namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using Reporters.ReportOutputs;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;
using System.Text;
using Microsoft.Extensions.Logging;

[TestFixture]
public class CsvReportWriterSpecialCharactersTests
{
    private StringWriter _stringWriter;
    private MethodOverrideManager _overrideManager;
    private List<ReportItem> _reportItems;
    private TestLogger<CsvReportWriterSpecialCharactersTests> _logger;
    private TestLoggerFactory<CsvReportWriterSpecialCharactersTests> _loggerFactory;
    private InMemoryFileSystem _fileSystem;
    private CsvUtils _csvUtils;

    [SetUp]
    public void Setup()
    {
        _logger = new TestLogger<CsvReportWriterSpecialCharactersTests>();
        _loggerFactory = new TestLoggerFactory<CsvReportWriterSpecialCharactersTests>(_logger);
        _loggerFactory.EnableLoggingFor<CsvReportWriter>();
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
    public void WriteReportItemsCsv_HandlesSpecialCharacters()
    {
        // Arrange
        _reportItems.Add(new ReportItem
        {
            Id = "1",
            MethodName = "Method,With,Commas",
            FullName = "Class.Method,With,Commas",
            StartTime = "2023-01-01 00:00:00.000",
            EndTime = "2023-01-01 00:00:01.000",
            Parameters = new Dictionary<string, string>
        {
            {"Param_With_Comma", "Value,With,Commas"},
            {"Param_With_Quotes", "Value \"With\" Quotes"},
        },
            AttributeParameters = new HashSet<string> { "Param_With_Comma", "Param_With_Quotes" }
        });

        var writer = new CsvReportWriter(_stringWriter, _reportItems, _overrideManager);

        // Act
        writer.WriteReportItemsCsv();

        // Assert
        var content = _stringWriter.ToString();
        _logger.LogInformation($"CSV Content:\n{content}");
        var lines = content.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.That(lines.Length, Is.EqualTo(2), "Should have header and one data line");
        Assert.That(lines[0], Does.Contain("Param_With_Comma"), "Header should contain Param_With_Comma");
        Assert.That(lines[1], Does.Contain("\"Method,With,Commas\""));
        Assert.That(lines[1], Does.Contain("\"Class.Method,With,Commas\""));
        Assert.That(lines[1], Does.Contain("\"Value,With,Commas\""));
        Assert.That(lines[1], Does.Contain("\"Value \"\"With\"\" Quotes\""));

        // Parse the CSV content
        var parsedItems = ParseCsvString(content);
        Assert.That(parsedItems, Has.Count.EqualTo(1));
        var parsedItem = parsedItems[0];

        Assert.That(parsedItem["Param_With_Comma"], Is.EqualTo("Value,With,Commas"));
        Assert.That(parsedItem["Param_With_Quotes"], Is.EqualTo("Value \"With\" Quotes"));
    }
    
    private List<Dictionary<string, string>> ParseCsvString(string csvContent)
    {
        var result = new List<Dictionary<string, string>>();
        var lines = csvContent.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        var headers = lines[0].Split(',');

        for (int i = 1; i < lines.Length; i++)
        {
            var values = SplitCsvLine(lines[i]);
            var item = new Dictionary<string, string>();
            for (int j = 0; j < headers.Length; j++)
            {
                item[headers[j]] = j < values.Length ? values[j] : string.Empty;
            }
            result.Add(item);
        }

        return result;
    }

    private string[] SplitCsvLine(string line)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (line[i] == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(line[i]);
            }
        }

        result.Add(current.ToString());
        return result.ToArray();
    }
}

namespace Orc.Monitoring;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orc.Monitoring.IO;

public class CsvUtils
{
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<CsvUtils> _logger;

    public CsvUtils(IFileSystem fileSystem, IMonitoringLoggerFactory loggerFactory)
    {
        _fileSystem = fileSystem;
        _logger = loggerFactory.CreateLogger<CsvUtils>();
    }

    public static CsvUtils Instance { get; } = new(FileSystem.Instance, MonitoringLoggerFactory.Instance);

    public async Task WriteCsvAsync(string filePath, IEnumerable<Dictionary<string, string>> data, string[] headers)
    {
        _logger.LogInformation("Writing CSV asynchronously to file: {FilePath}", filePath);
        var buffer = new StringBuilder();
        buffer.AppendJoin(',', headers.Select(EscapeCsvValue));
        foreach (var item in data)
        {
            var values = headers.Select(h => item[h]).ToArray();
            buffer.AppendLine();
            buffer.AppendJoin(',', values.Select(EscapeCsvValue));
        }
        await _fileSystem.WriteAllTextAsync(filePath, buffer.ToString());
    }

    public List<Dictionary<string, string>> ReadCsv(string filePath)
    {
        _logger.LogInformation("Reading CSV from file: {FilePath}", filePath);
        var result = new List<Dictionary<string, string>>();
        using var reader = _fileSystem.CreateStreamReader(filePath);

        var headerLine = reader.ReadLine();
        if (headerLine is null)
        {
            _logger.LogWarning("The CSV file is empty.");
            return result;
        }

        var headers = ParseCsvLine(headerLine);

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var values = ParseCsvLine(line);
            if (values.Length != headers.Length)
            {
                _logger.LogWarning("Skipping line due to mismatched column count: {Line}", line);
                continue;
            }

            var row = new Dictionary<string, string>(headers.Length);
            for (int i = 0; i < headers.Length; i++)
            {
                row[headers[i]] = values[i];
            }
            result.Add(row);
        }

        return result;
    }

    public async Task<List<Dictionary<string, string>>> ReadCsvAsync(string filePath)
    {
        _logger.LogInformation("Reading CSV asynchronously from file: {FilePath}", filePath);
        var result = new List<Dictionary<string, string>>();
        using var reader = _fileSystem.CreateStreamReader(filePath);

        var headerLine = await reader.ReadLineAsync();
        if (headerLine is null)
        {
            _logger.LogWarning("The CSV file is empty.");
            return result;
        }

        var headers = ParseCsvLine(headerLine);

        string? line;
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            var values = ParseCsvLine(line);
            if (values.Length != headers.Length)
            {
                _logger.LogWarning("Skipping line due to mismatched column count: {Line}", line);
                continue;
            }

            var row = new Dictionary<string, string>(headers.Length);
            for (int i = 0; i < headers.Length; i++)
            {
                row[headers[i]] = values[i];
            }
            result.Add(row);
        }

        return result;
    }

    public string EscapeCsvValue(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        bool mustQuote = value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r') || value.StartsWith(' ') || value.EndsWith(' ');

        if (mustQuote)
        {
            var escapedValue = value.Replace("\"", "\"\"");
            return $"\"{escapedValue}\"";
        }
        return value;
    }

    public string[] ParseCsvLine(string line)
    {
        var result = new List<string>();
        var currentValue = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                if (inQuotes)
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        // Escaped quote
                        currentValue.Append('"');
                        i++; // Skip the next quote
                    }
                    else
                    {
                        // Closing quote
                        inQuotes = false;
                    }
                }
                else
                {
                    // Opening quote
                    inQuotes = true;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                // Field delimiter
                result.Add(currentValue.ToString());
                currentValue.Clear();
            }
            else
            {
                // Regular character
                currentValue.Append(c);
            }
        }

        if (inQuotes)
        {
            throw new FormatException("Malformed CSV line: unmatched quote.");
        }

        result.Add(currentValue.ToString());
        return result.ToArray();
    }
}


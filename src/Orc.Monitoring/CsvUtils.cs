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
        this._fileSystem = fileSystem;
        this._logger = loggerFactory.CreateLogger<CsvUtils>();
    }

    public static CsvUtils Instance { get; } = new(FileSystem.Instance, MonitoringLoggerFactory.Instance);

    public void WriteCsvLine(TextWriter writer, string?[] values)
    {
        var line = string.Join(",", values.Select(EscapeCsvValue));
        _logger.LogDebug("Writing CSV line: {Line}", line);
        writer.WriteLine(line);
    }

    public async Task WriteCsvLineAsync(TextWriter writer, string?[] values)
    {
        var line = string.Join(",", values.Select(EscapeCsvValue));
        _logger.LogDebug("Writing CSV line asynchronously: {Line}", line);
        await writer.WriteLineAsync(line);
    }

    public void WriteCsv<T>(string filePath, IEnumerable<T> data, string[] headers)
    {
        _logger.LogInformation("Writing CSV to file: {FilePath}", filePath);
        using var writer = _fileSystem.CreateStreamWriter(filePath, false, Encoding.UTF8);

        WriteCsvLine(writer, headers.Cast<string?>().ToArray());

        foreach (var item in data)
        {
            var values = headers.Select(h => GetPropertyValue(item, h)?.ToString()).ToArray();
            WriteCsvLine(writer, values);
        }
    }

    public async Task WriteCsvAsync<T>(string filePath, IEnumerable<T> data, string[] headers)
    {
        _logger.LogInformation("Writing CSV asynchronously to file: {FilePath}", filePath);
        await using var writer = _fileSystem.CreateStreamWriter(filePath, false, Encoding.UTF8);

        await WriteCsvLineAsync(writer, headers.Cast<string?>().ToArray());

        foreach (var item in data)
        {
            var values = headers.Select(h => GetPropertyValue(item, h)?.ToString()).ToArray();
            await WriteCsvLineAsync(writer, values);
        }
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

    private object? GetPropertyValue(object? obj, string propertyName)
    {
        if (obj is null)
        {
            return null;
        }

        if (obj is IDictionary<string, string> dict)
        {
            return dict.TryGetValue(propertyName, out var value) ? value : null;
        }
        return obj.GetType().GetProperty(propertyName)?.GetValue(obj, null);
    }
}


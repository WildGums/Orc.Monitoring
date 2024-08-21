namespace Orc.Monitoring;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public static class CsvUtils
{
    public static void WriteCsvLine(TextWriter writer, string?[] values)
    {
        writer.WriteLine(string.Join(",", values.Select(EscapeCsvValue)));
    }

    public static async Task WriteCsvLineAsync(TextWriter writer, string?[] values)
    {
        await writer.WriteLineAsync(string.Join(",", values.Select(EscapeCsvValue)));
    }

    public static void WriteCsv<T>(string filePath, IEnumerable<T> data, string[] headers)
    {
        using var writer = new StreamWriter(filePath, false, Encoding.UTF8);

        WriteCsvLine(writer, headers.Cast<string?>().ToArray());

        foreach (var item in data)
        {
            var values = headers.Select(h => GetPropertyValue(item, h)?.ToString()).ToArray();
            WriteCsvLine(writer, values);
        }
    }

    public static async Task WriteCsvAsync<T>(string filePath, IEnumerable<T> data, string[] headers)
    {
        using var writer = new StreamWriter(filePath, false, Encoding.UTF8);

        await WriteCsvLineAsync(writer, headers.Cast<string?>().ToArray());

        foreach (var item in data)
        {
            var values = headers.Select(h => GetPropertyValue(item, h)?.ToString()).ToArray();
            await WriteCsvLineAsync(writer, values);
        }
    }

    public static List<Dictionary<string, string>> ReadCsv(string filePath)
    {
        var result = new List<Dictionary<string, string>>();
        using var reader = new StreamReader(filePath);

        var headers = ParseCsvLine(reader.ReadLine() ?? string.Empty);

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var values = ParseCsvLine(line);
            if (values.Length != headers.Length) continue;

            var row = new Dictionary<string, string>(headers.Length);
            for (int i = 0; i < headers.Length; i++)
            {
                row[headers[i]] = values[i];
            }
            result.Add(row);
        }

        return result;
    }

    public static async Task<List<Dictionary<string, string>>> ReadCsvAsync(string filePath)
    {
        var result = new List<Dictionary<string, string>>();
        using var reader = new StreamReader(filePath);

        var headerLine = await reader.ReadLineAsync();
        var headers = ParseCsvLine(headerLine ?? string.Empty);

        string? line;
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            var values = ParseCsvLine(line);
            if (values.Length != headers.Length) continue;

            var row = new Dictionary<string, string>(headers.Length);
            for (int i = 0; i < headers.Length; i++)
            {
                row[headers[i]] = values[i];
            }
            result.Add(row);
        }

        return result;
    }

    public static string? EscapeCsvValue(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }

    private static string[] ParseCsvLine(string line)
    {
        var result = new List<string>();
        var currentValue = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    currentValue.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (line[i] == ',' && !inQuotes)
            {
                result.Add(currentValue.ToString());
                currentValue.Clear();
            }
            else
            {
                currentValue.Append(line[i]);
            }
        }

        result.Add(currentValue.ToString());
        return result.ToArray();
    }

    private static object? GetPropertyValue(object? obj, string propertyName)
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

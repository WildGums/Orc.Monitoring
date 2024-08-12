namespace Orc.Monitoring.Reporters.ReportOutputs;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;


public class MethodOverrideManager
{
    private readonly string _overrideFilePath;
    private readonly Dictionary<string, Dictionary<string, string>> _overrides;
    private HashSet<string> _customColumns;

    public MethodOverrideManager(string? outputDirectory)
    {
        ArgumentNullException.ThrowIfNull(outputDirectory);

        _overrideFilePath = Path.Combine(outputDirectory, "method_overrides.csv");
        _overrides = new Dictionary<string, Dictionary<string, string>>();
        _customColumns = new HashSet<string>();
    }

    public void LoadOverrides()
    {
        if (!File.Exists(_overrideFilePath))
        {
            return;
        }

        using var reader = new StreamReader(_overrideFilePath);
        var headers = ParseCsvLine(reader.ReadLine() ?? string.Empty);

        if (headers.Length == 0)
        {
            return;
        }

        _customColumns = new HashSet<string>(headers.Where(h => h != "FullName"));

        while (!reader.EndOfStream)
        {
            var values = ParseCsvLine(reader.ReadLine() ?? string.Empty);
            if (values.Length != headers.Length)
            {
                continue;
            }

            var fullName = values[0];
            var methodOverrides = new Dictionary<string, string>();

            for (int i = 1; i < headers.Length; i++)
            {
                if (!string.IsNullOrEmpty(values[i]))
                {
                    methodOverrides[headers[i]] = values[i];
                }
            }

            if (methodOverrides.Count > 0)
            {
                _overrides[fullName] = methodOverrides;
            }
        }
    }

    public void SaveOverrides(ICollection<ReportItem> reportItems)
    {
        var allStaticParameters = new HashSet<string>(reportItems
            .SelectMany(item => item.AttributeParameters)
            .Distinct());

        var allColumns = new HashSet<string>(_customColumns.Union(allStaticParameters));

        var uniqueReportItems = reportItems
            .GroupBy(item => item.FullName)
            .Select(group => group.Last())
            .ToList();

        using var writer = new StreamWriter(_overrideFilePath, false, Encoding.UTF8);

        // Write header
        writer.WriteLine(ToCsvLine(new[] { "FullName" }.Concat(allColumns.OrderBy(c => c)).ToArray()));

        // Write data rows
        foreach (var item in uniqueReportItems)
        {
            var fullName = item.FullName ?? string.Empty;
            var row = new List<string> { fullName };

            foreach (var column in allColumns.OrderBy(c => c))
            {
                if (_overrides.TryGetValue(fullName, out var methodOverrides) && methodOverrides.TryGetValue(column, out var overrideValue))
                {
                    row.Add(overrideValue);
                }
                else if (item.Parameters.TryGetValue(column, out var value) && item.AttributeParameters.Contains(column))
                {
                    row.Add(value);
                }
                else
                {
                    row.Add(string.Empty);
                }
            }

            writer.WriteLine(ToCsvLine(row.ToArray()));
        }

        // Preserve any existing rows that weren't in the reportItems
        foreach (var fullName in _overrides.Keys.Except(uniqueReportItems.Select(i => i.FullName)))
        {
            var row = new List<string> { fullName ?? string.Empty };

            foreach (var column in allColumns.OrderBy(c => c))
            {
                row.Add(_overrides[fullName ?? string.Empty].TryGetValue(column, out var value) ? value : string.Empty);
            }

            writer.WriteLine(ToCsvLine(row.ToArray()));
        }
    }

    private string[] ParseCsvLine(string line)
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
                    // Escaped quote
                    currentValue.Append('"');
                    i++;
                }
                else
                {
                    // Toggle quote mode
                    inQuotes = !inQuotes;
                }
            }
            else if (line[i] == ',' && !inQuotes)
            {
                // End of field
                result.Add(currentValue.ToString());
                currentValue.Clear();
            }
            else
            {
                currentValue.Append(line[i]);
            }
        }

        // Add the last field
        result.Add(currentValue.ToString());

        return result.ToArray();
    }

    private string ToCsvLine(string[] values)
    {
        return string.Join(",", values.Select(EscapeCsvValue));
    }

    private string EscapeCsvValue(string value)
    {
        if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }

    public Dictionary<string, string> GetOverridesForMethod(string fullName)
    {
        return _overrides.TryGetValue(fullName, out var methodOverrides) ? methodOverrides : new Dictionary<string, string>();
    }

    public IEnumerable<string> GetCustomColumns()
    {
        return _customColumns;
    }

    public void AddCustomColumn(string columnName)
    {
        _customColumns.Add(columnName);
    }
}

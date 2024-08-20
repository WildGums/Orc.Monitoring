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

        var overrides = CsvUtils.ReadCsv(_overrideFilePath);
        if (!overrides.Any())
        {
            // If the file is empty, just clear any existing overrides and return
            _overrides.Clear();
            _customColumns.Clear();
            return;
        }

        var headers = overrides.First().Keys.ToArray();

        _customColumns = new HashSet<string>(headers.Where(h => h != "FullName"));

        foreach (var row in overrides)
        {
            var fullName = row["FullName"];
            var methodOverrides = new Dictionary<string, string>();

            foreach (var header in headers.Where(h => h != "FullName"))
            {
                if (!string.IsNullOrEmpty(row[header]))
                {
                    methodOverrides[header] = row[header];
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
        var allStaticParameters = new HashSet<string>(reportItems.SelectMany(item => item.AttributeParameters));
        _customColumns = new HashSet<string>(_customColumns.Union(allStaticParameters));
        var allColumns = new HashSet<string>(_customColumns);
        var headers = new[] { "FullName", "IsStatic", "IsExtension" }.Concat(allColumns.OrderBy(c => c)).ToArray();

        using var writer = new StreamWriter(_overrideFilePath, false, Encoding.UTF8);
        CsvUtils.WriteCsvLine(writer, headers);

        foreach (var item in reportItems)
        {
            WritePreparedOverrideRow(writer, PrepareOverrideRow(item, allColumns), headers);

            // Update _overrides dictionary
            var fullName = item.FullName ?? string.Empty;
            if (!_overrides.ContainsKey(fullName))
            {
                _overrides[fullName] = new Dictionary<string, string>();
            }
            foreach (var param in item.Parameters)
            {
                if (item.AttributeParameters.Contains(param.Key))
                {
                    _overrides[fullName][param.Key] = param.Value;
                }
            }
        }

        foreach (var fullName in _overrides.Keys.Except(reportItems.Select(i => i.FullName)).Where(fn => fn is not null))
        {
            WritePreparedOverrideRow(writer, PrepareExistingOverrideRow(fullName!, allColumns), headers);
        }
    }

    private void WritePreparedOverrideRow(TextWriter writer, Dictionary<string, string> row, string[] headers)
    {
        CsvUtils.WriteCsvLine(writer, headers.Select(h => row.TryGetValue(h, out var value) ? value : string.Empty).ToArray());
    }

    private Dictionary<string,string> PrepareOverrideRow(ReportItem item, IEnumerable<string> allColumns)
    {
        var fullName = item.FullName ?? string.Empty;
        var row = new Dictionary<string, string>
        {
            ["FullName"] = fullName,
            ["IsStatic"] = GetPropertyOrDefault(item, "IsStatic", false).ToString(),
            ["IsExtension"] = GetPropertyOrDefault(item, "IsExtension", false).ToString()
        };

        foreach (var column in allColumns)
        {
            if (_overrides.TryGetValue(fullName, out var methodOverrides) && methodOverrides.TryGetValue(column, out var overrideValue))
            {
                row[column] = overrideValue;
            }
            else if (item.Parameters.TryGetValue(column, out var value) && item.AttributeParameters.Contains(column))
            {
                row[column] = value;
            }
            else
            {
                row[column] = string.Empty;
            }
        }

        return row;
    }

    private Dictionary<string,string> PrepareExistingOverrideRow(string fullName, IEnumerable<string> allColumns)
    { 
        var row = new Dictionary<string, string>
        {
            ["FullName"] = fullName,
            ["IsStatic"] = "False",
            ["IsExtension"] = "False"
        };

        foreach (var column in allColumns)
        {
            row[column] = _overrides[fullName].TryGetValue(column, out var value) ? value : string.Empty;
        }

        return row;
    }

    public Dictionary<string, string> GetOverridesForMethod(string fullName)
    {
        return _overrides.TryGetValue(fullName, out var methodOverrides) ? methodOverrides : new Dictionary<string, string>();
    }

    public IEnumerable<string> GetCustomColumns()
    {
        return _customColumns;
    }

    private static T GetPropertyOrDefault<T>(ReportItem item, string propertyName, T defaultValue)
    {
        if (item.Parameters.TryGetValue(propertyName, out var value))
        {
            if (typeof(T) == typeof(bool) && bool.TryParse(value, out var boolValue))
            {
                return (T)(object)boolValue;
            }
            // Add more type checks if needed for other property types
        }
        return defaultValue;
    }
}

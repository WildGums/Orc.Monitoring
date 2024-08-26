using Orc.Monitoring.Reporters.ReportOutputs;
using Orc.Monitoring;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System;

public class MethodOverrideManager
{
    private readonly string _overrideFilePath;
    private readonly Dictionary<string, Dictionary<string, string>> _overrides;
    private HashSet<string> _customColumns;
    private readonly object _saveLock = new object();

    public MethodOverrideManager(string? outputDirectory)
    {
        ArgumentNullException.ThrowIfNull(outputDirectory);

        _overrideFilePath = Path.Combine(outputDirectory, "method_overrides.csv");
        _overrides = new Dictionary<string, Dictionary<string, string>>();
        _customColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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

        _customColumns = new HashSet<string>(headers.Where(h => h != "FullName" && h != "IsStatic" && h != "IsExtension"), StringComparer.OrdinalIgnoreCase);

        foreach (var row in overrides)
        {
            var fullName = row["FullName"];
            var methodOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

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
        lock (_saveLock)
        {
            foreach (var item in reportItems)
            {
                _customColumns.UnionWith(item.AttributeParameters);
                _customColumns.UnionWith(item.Parameters.Keys);
            }

            var headers = new[] { "FullName", "IsStatic", "IsExtension" }.Concat(_customColumns.OrderBy(c => c)).ToArray();

            using var writer = new StreamWriter(_overrideFilePath, false, Encoding.UTF8);
            CsvUtils.WriteCsvLine(writer, headers);

            foreach (var item in reportItems)
            {
                WritePreparedOverrideRow(writer, PrepareOverrideRow(item), headers);

                // Update _overrides dictionary
                var fullName = item.FullName ?? string.Empty;
                if (!_overrides.TryGetValue(fullName, out var methodOverrides))
                {
                    methodOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    _overrides[fullName] = methodOverrides;
                }

                foreach (var param in item.Parameters)
                {
                    if (item.AttributeParameters.Contains(param.Key))
                    {
                        methodOverrides[param.Key] = param.Value;
                    }
                }
            }

            foreach (var fullName in _overrides.Keys.Except(reportItems.Select(i => i.FullName)).Where(fn => fn is not null))
            {
                WritePreparedOverrideRow(writer, PrepareExistingOverrideRow(fullName!), headers);
            }
        }
    }

    private Dictionary<string, string> PrepareOverrideRow(ReportItem item)
    {
        var fullName = item.FullName ?? string.Empty;
        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["FullName"] = fullName,
            ["IsStatic"] = GetPropertyOrDefault(item, "IsStatic", false).ToString(),
            ["IsExtension"] = GetPropertyOrDefault(item, "IsExtension", false).ToString()
        };

        foreach (var column in _customColumns)
        {
            if (item.Parameters.TryGetValue(column, out var value))
            {
                row[column] = value;
            }
            else if (_overrides.TryGetValue(fullName, out var methodOverrides) && methodOverrides.TryGetValue(column, out var overrideValue))
            {
                row[column] = overrideValue;
            }
            else
            {
                row[column] = string.Empty;
            }
        }

        return row;
    }

    private Dictionary<string, string> PrepareExistingOverrideRow(string fullName)
    {
        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["FullName"] = fullName,
            ["IsStatic"] = "False",
            ["IsExtension"] = "False"
        };

        if (_overrides.TryGetValue(fullName, out var methodOverrides))
        {
            foreach (var column in _customColumns)
            {
                if (methodOverrides.TryGetValue(column, out var value))
                {
                    row[column] = value;
                }
                else
                {
                    row[column] = string.Empty;
                }
            }
        }

        return row;
    }

    private void WritePreparedOverrideRow(TextWriter writer, Dictionary<string, string> row, string[] headers)
    {
        CsvUtils.WriteCsvLine(writer, headers.Select(h => row.TryGetValue(h, out var value) ? value : string.Empty).ToArray());
    }

    private static T GetPropertyOrDefault<T>(ReportItem item, string propertyName, T defaultValue)
    {
        if (item.Parameters.TryGetValue(propertyName, out var value))
        {
            if (typeof(T) == typeof(bool) && bool.TryParse(value, out var boolValue))
            {
                return (T)(object)boolValue;
            }
        }
        return defaultValue;
    }

    public Dictionary<string, string> GetOverridesForMethod(string fullName)
    {
        return _overrides.TryGetValue(fullName, out var methodOverrides) ? methodOverrides : new Dictionary<string, string>();
    }

    public IEnumerable<string> GetCustomColumns()
    {
        return _customColumns;
    }
}

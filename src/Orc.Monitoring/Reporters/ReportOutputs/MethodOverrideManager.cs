namespace Orc.Monitoring.Reporters.ReportOutputs;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using IO;
using Microsoft.Extensions.Logging;
using Orc.Monitoring;

public class MethodOverrideManager
{
    private readonly IFileSystem _fileSystem;
    private readonly CsvUtils _csvUtils;
    private readonly string _overrideFilePath;
    private readonly string _overrideTemplateFilePath;
    private readonly Dictionary<string, Dictionary<string, string>> _overrides;
    private readonly HashSet<string> _customColumns;
    private readonly HashSet<string> _obsoleteColumns;
    private readonly object _saveLock = new object();
    private readonly ILogger<MethodOverrideManager> _logger;
    
    public MethodOverrideManager(string outputDirectory, IMonitoringLoggerFactory loggerFactory, IFileSystem fileSystem, CsvUtils csvUtils)
    {
        ArgumentNullException.ThrowIfNull(outputDirectory);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(csvUtils);

        _fileSystem = fileSystem;
        _csvUtils = csvUtils;
        _overrideFilePath = Path.Combine(outputDirectory, "method_overrides.csv");
        _overrideTemplateFilePath = Path.Combine(outputDirectory, "method_overrides.template");
        _overrides = new Dictionary<string, Dictionary<string, string>>();
        _customColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _logger = loggerFactory.CreateLogger<MethodOverrideManager>();
        _obsoleteColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    }

    public void LoadOverrides()
    {
        if (!_fileSystem.FileExists(_overrideFilePath))
        {
            _logger.LogInformation($"Override file not found: {_overrideFilePath}");
            return;
        }

        var overrides = _csvUtils.ReadCsv(_overrideFilePath);
        if (!overrides.Any())
        {
            _overrides.Clear();
            _customColumns.Clear();
            _obsoleteColumns.Clear();
            return;
        }

        var headers = overrides.First().Keys.ToArray();
        var newCustomColumns = new HashSet<string>(
            headers.Where(h => h != "FullName" && h != "IsStatic" && h != "IsExtension"),
            StringComparer.OrdinalIgnoreCase
        );

        // Determine obsolete columns
        _obsoleteColumns.Clear();
        _obsoleteColumns.UnionWith(_customColumns.Except(newCustomColumns));

        // Update custom columns
        _customColumns.Clear();
        _customColumns.UnionWith(newCustomColumns);

        _overrides.Clear();
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

        _logger.LogInformation($"Loaded {_overrides.Count} method overrides from {_overrideFilePath}");
    }

    public void SaveOverrides(ICollection<ReportItem> reportItems)
    {
        lock (_saveLock)
        {
            var currentCustomColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in reportItems)
            {
                currentCustomColumns.UnionWith(item.AttributeParameters);
                currentCustomColumns.UnionWith(item.Parameters.Keys);
            }

            // Add new columns to _customColumns
            _customColumns.UnionWith(currentCustomColumns);

            // Mark columns not in current items as obsolete
            _obsoleteColumns.UnionWith(_customColumns.Except(currentCustomColumns));

            var headers = new[] { "FullName", "IsStatic", "IsExtension" }.Concat(_customColumns.OrderBy(c => c)).ToArray();

            using var writer = _fileSystem.CreateStreamWriter(_overrideTemplateFilePath, false, Encoding.UTF8);
            _csvUtils.WriteCsvLine(writer, headers);

            foreach (var item in reportItems)
            {
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

                WritePreparedOverrideRow(writer, PrepareOverrideRow(fullName, methodOverrides), headers);
            }

            // Remove any methods that are no longer present in the report items
            var reportItemFullNames = new HashSet<string>(reportItems.Select(i => i.FullName ?? string.Empty));
            foreach (var fullName in _overrides.Keys.ToList())
            {
                if (!reportItemFullNames.Contains(fullName))
                {
                    _overrides.Remove(fullName);
                }
            }

            _logger.LogInformation($"Saved method override template to {_overrideTemplateFilePath}");
        }
    }

    public void CleanupObsoleteColumns()
    {
        lock (_saveLock)
        {
            // Remove obsolete columns from _customColumns
            _customColumns.ExceptWith(_obsoleteColumns);

            // Remove obsolete columns from all method overrides
            foreach (var methodOverrides in _overrides.Values)
            {
                foreach (var obsoleteColumn in _obsoleteColumns)
                {
                    methodOverrides.Remove(obsoleteColumn);
                }
            }

            // Clear the obsolete columns set
            _obsoleteColumns.Clear();

            // Rewrite the template file with cleaned-up data
            var cleanedReportItems = _overrides.Select(kvp => new ReportItem
            {
                FullName = kvp.Key,
                Parameters = kvp.Value,
                AttributeParameters = new HashSet<string>(kvp.Value.Keys)
            }).ToList();

            SaveOverrides(cleanedReportItems);
        }
    }

    private Dictionary<string, string> PrepareOverrideRow(string fullName, Dictionary<string, string> overrides)
    {
        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["FullName"] = fullName,
            ["IsStatic"] = "False",
            ["IsExtension"] = "False"
        };

        foreach (var column in _customColumns)
        {
            if (overrides.TryGetValue(column, out var value))
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
        _csvUtils.WriteCsvLine(writer, headers.Select(h => row.TryGetValue(h, out var value) ? value : string.Empty).ToArray());
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

namespace Orc.Monitoring.Reporters.ReportOutputs;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;

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
        using var csv = new CsvReader(reader, new CsvConfiguration(System.Globalization.CultureInfo.InvariantCulture));

        csv.Read();
        csv.ReadHeader();

        _customColumns = new HashSet<string>(csv.HeaderRecord?.Where(h => h != "FullName") ?? new List<string>());

        while (csv.Read())
        {
            var fullName = csv.GetField("FullName") ?? string.Empty;
            var methodOverrides = new Dictionary<string, string>();

            foreach (var header in _customColumns)
            {
                var value = csv.GetField(header);
                if (!string.IsNullOrEmpty(value))
                {
                    methodOverrides[header] = value;
                }
            }

            if (methodOverrides.Count > 0)
            {
                _overrides[fullName] = methodOverrides;
            }
        }
    }

    public void SaveOverrides(IEnumerable<ReportItem> reportItems)
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
        using var csv = new CsvWriter(writer, new CsvConfiguration(System.Globalization.CultureInfo.InvariantCulture));

        csv.WriteField("FullName");
        foreach (var column in allColumns.OrderBy(c => c))
        {
            csv.WriteField(column);
        }
        csv.NextRecord();

        foreach (var item in uniqueReportItems)
        {
            var fullName = item.FullName ?? string.Empty;
            csv.WriteField(fullName);
            foreach (var column in allColumns.OrderBy(c => c))
            {
                if (_overrides.TryGetValue(fullName, out var methodOverrides) && methodOverrides.TryGetValue(column, out var overrideValue))
                {
                    csv.WriteField(overrideValue);
                }
                else if (item.Parameters.TryGetValue(column, out var value) && item.AttributeParameters.Contains(column))
                {
                    csv.WriteField(value);
                }
                else
                {
                    csv.WriteField(string.Empty);
                }
            }
            csv.NextRecord();
        }

        // Preserve any existing rows that weren't in the reportItems
        foreach (var fullName in _overrides.Keys.Except(uniqueReportItems.Select(i => i.FullName)))
        {
            csv.WriteField(fullName);
            foreach (var column in allColumns.OrderBy(c => c))
            {
                csv.WriteField(_overrides[fullName ?? string.Empty].TryGetValue(column, out var value) 
                    ? value 
                    : string.Empty);
            }
            csv.NextRecord();
        }
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

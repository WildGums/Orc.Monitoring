namespace Orc.Monitoring.Reporters.ReportOutputs;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class CsvReportWriter
{
    private readonly TextWriter _writer;
    private readonly IEnumerable<ReportItem> _reportItems;
    private readonly MethodOverrideManager _overrideManager;
    private readonly HashSet<string> _customColumns;

    public CsvReportWriter(TextWriter writer, IEnumerable<ReportItem> reportItems, MethodOverrideManager overrideManager)
    {
        _writer = writer;
        _reportItems = reportItems;
        _overrideManager = overrideManager;
        _customColumns = new HashSet<string>(overrideManager.GetCustomColumns());
    }

    public void WriteReportItemsCsv()
    {
        var headers = GetReportItemHeaders();
        CsvUtils.WriteCsvLine(_writer, headers);

        foreach (var item in PrepareReportItems())
        {
            var values = headers.Select(h => item.TryGetValue(h, out var value) ? value : string.Empty).ToArray();
            CsvUtils.WriteCsvLine(_writer, values);
        }
    }

    private IEnumerable<Dictionary<string, string>> PrepareReportItems()
    {
        return _reportItems
            .Where(item => !string.IsNullOrEmpty(item.StartTime))
            .OrderBy(item => DateTime.Parse(item.StartTime ?? string.Empty))
            .ThenBy(item => item.ThreadId)
            .ThenBy(item => item.Level)
            .Select(PrepareReportItem);
    }

    private Dictionary<string, string> PrepareReportItem(ReportItem item)
    {
        var result = new Dictionary<string, string>
        {
            ["Id"] = item.Id ?? string.Empty,
            ["ParentId"] = item.Parent ?? "ROOT",
            ["StartTime"] = item.StartTime ?? string.Empty,
            ["EndTime"] = item.EndTime ?? string.Empty,
            ["Report"] = item.Report ?? string.Empty,
            ["ClassName"] = item.ClassName ?? string.Empty,
            ["MethodName"] = item.MethodName ?? string.Empty,
            ["FullName"] = item.FullName ?? string.Empty,
            ["Duration"] = item.Duration ?? string.Empty,
            ["ThreadId"] = item.ThreadId ?? string.Empty,
            ["ParentThreadId"] = item.ParentThreadId ?? string.Empty,
            ["NestingLevel"] = item.Level ?? string.Empty,
            ["IsStatic"] = GetPropertyOrDefault(item, "IsStatic", false).ToString(),
            ["IsGeneric"] = GetPropertyOrDefault(item, "IsGeneric", false).ToString(),
            ["IsExtension"] = GetPropertyOrDefault(item, "IsExtension", false).ToString()
        };

        var fullName = item.FullName ?? string.Empty;
        var overrides = _overrideManager.GetOverridesForMethod(fullName);
        var parameters = new Dictionary<string, string>(item.Parameters);

        foreach (var kvp in overrides)
        {
            parameters[kvp.Key] = kvp.Value;
        }

        foreach (var param in parameters)
        {
            result[param.Key] = param.Value;
        }

        return result;
    }

    public void WriteRelationshipsCsv(string filePath)
    {
        var headers = new[] { "From", "To", "RelationType" };
        var relationships = _reportItems
            .Where(r => !string.IsNullOrEmpty(r.Parent))
            .Select(item => new
            {
                From = item.Parent,
                To = item.Id,
                RelationType = DetermineRelationType(item)
            });

        CsvUtils.WriteCsv(filePath, relationships, headers);
    }

    private string[] GetReportItemHeaders()
    {
        var baseHeaders = new[] { "Id", "ParentId", "StartTime", "EndTime", "Report", "ClassName", "MethodName", "FullName", "Duration", "ThreadId", "ParentThreadId", "NestingLevel", "IsStatic", "IsGeneric", "IsExtension" };
        var parameterHeaders = _reportItems.SelectMany(r => r.Parameters.Keys).Distinct().Where(k => !_customColumns.Contains(k));
        return baseHeaders.Concat(_customColumns).Concat(parameterHeaders).ToArray();
    }

    private string DetermineRelationType(ReportItem item)
    {
        if (GetPropertyOrDefault(item, "IsStatic", false))
            return "Static";
        if (GetPropertyOrDefault(item, "IsExtension", false))
            return "Extension";
        if (GetPropertyOrDefault(item, "IsGeneric", false))
            return "Generic";
        return "Regular";
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

    private string SanitizeHeaderName(string headerName)
    {
        return headerName.Replace(",", "_").Replace("\"", "_").Replace("\n", "_").Replace("\r", "_");
    }
}

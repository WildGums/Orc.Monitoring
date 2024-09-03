namespace Orc.Monitoring.Reporters.ReportOutputs;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

public class CsvReportWriter
{
    private readonly TextWriter _writer;
    private readonly IEnumerable<ReportItem> _reportItems;
    private readonly MethodOverrideManager _overrideManager;
    private readonly ILogger<CsvReportWriter> _logger;
    private readonly CsvUtils _csvUtils;

    public CsvReportWriter(TextWriter writer, IEnumerable<ReportItem> reportItems, MethodOverrideManager methodOverrideManager)
    : this(writer, reportItems, methodOverrideManager, MonitoringLoggerFactory.Instance, CsvUtils.Instance)
    {
    }

    public CsvReportWriter(TextWriter writer, IEnumerable<ReportItem> reportItems, MethodOverrideManager overrideManager, IMonitoringLoggerFactory loggerFactory, CsvUtils csvUtils)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(reportItems);
        ArgumentNullException.ThrowIfNull(overrideManager);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _writer = writer;
        _reportItems = reportItems;
        _overrideManager = overrideManager;
        _logger = loggerFactory.CreateLogger<CsvReportWriter>();
        _csvUtils = csvUtils;
    }

    public void WriteReportItemsCsv()
    {
        var headers = GetReportItemHeaders();
        _csvUtils.WriteCsvLine(_writer, headers.Cast<string?>().ToArray());

        foreach (var item in PrepareReportItems())
        {
            var values = headers.Select(h => item.TryGetValue(h, out var value) ? value : null).ToArray();
            _csvUtils.WriteCsvLine(_writer, values);
        }

        _logger.LogInformation($"Wrote {_reportItems.Count()} report items to CSV");
    }

    private string EscapeCsvContent(string? content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return string.Empty;
        }

        content = content.Replace("\"", "\"\"");
        if (content.Contains(",") || content.Contains("\"") || content.Contains("\n") || content.Contains("<") || content.Contains(">") || content.Contains("&"))
        {
            return $"\"{content}\"";
        }
        return content;
    }

    public async Task WriteReportItemsCsvAsync()
    {
        var headers = GetReportItemHeaders();
        await _csvUtils.WriteCsvLineAsync(_writer, headers.Cast<string?>().Select(EscapeCsvContent).ToArray());

        var items = PrepareReportItems().ToList();
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var values = headers.Select(h => EscapeCsvContent(item.TryGetValue(h, out var value) ? value : null)).ToArray();

            if (i == items.Count - 1)
            {
                // For the last item, write without a newline
                await _writer.WriteAsync(string.Join(",", values));
            }
            else
            {
                await _csvUtils.WriteCsvLineAsync(_writer, values);
            }
        }

        _logger.LogInformation($"Wrote {items.Count} report items to CSV asynchronously");
    }

    public void WriteRelationshipsCsv()
    {
        var headers = new string?[] { "From", "To", "RelationType" };
        _csvUtils.WriteCsvLine(_writer, headers);

        var relationships = _reportItems
            .Where(r => !string.IsNullOrEmpty(r.Parent))
            .Select(item => new
            {
                From = item.Parent,
                To = item.Id,
                RelationType = DetermineRelationType(item)
            });

        foreach (var relationship in relationships)
        {
            _csvUtils.WriteCsvLine(_writer, new string?[] { relationship.From, relationship.To, relationship.RelationType });
        }

        _logger.LogInformation($"Wrote {relationships.Count()} relationships to CSV");
    }

    public async Task WriteRelationshipsCsvAsync()
    {
        var headers = new string?[] { "From", "To", "RelationType" };
        await _csvUtils.WriteCsvLineAsync(_writer, headers);

        var relationships = _reportItems
            .Where(r => !string.IsNullOrEmpty(r.Parent))
            .Select(item => new
            {
                From = item.Parent,
                To = item.Id,
                RelationType = DetermineRelationType(item)
            });

        foreach (var relationship in relationships)
        {
            await _csvUtils.WriteCsvLineAsync(_writer, new string?[] { relationship.From, relationship.To, relationship.RelationType });
        }

        _logger.LogInformation($"Wrote {relationships.Count()} relationships to CSV asynchronously");
    }

    private IEnumerable<Dictionary<string, string>> PrepareReportItems()
    {
        var rootItem = _reportItems.FirstOrDefault(item => item.Id == "ROOT");
        var nonRootItems = _reportItems
            .Where(item => item.Id != "ROOT" && !string.IsNullOrEmpty(item.StartTime))
            .OrderBy(item => DateTime.Parse(item.StartTime ?? string.Empty))
            .ThenBy(item => item.ThreadId)
            .ThenBy(item => item.Level);

        var allItems = new List<ReportItem>();
        if (rootItem is not null)
        {
            allItems.Add(rootItem);
        }
        allItems.AddRange(nonRootItems);

        return allItems.Select(PrepareReportItem);
    }

    private Dictionary<string, string> PrepareReportItem(ReportItem item)
    {
        var result = new Dictionary<string, string>
        {
            ["Id"] = item.Id ?? string.Empty,
            ["ParentId"] = item.Id == "ROOT" ? string.Empty : (item.Parent ?? "ROOT"),
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

    private string[] GetReportItemHeaders()
    {
        var baseHeaders = new[] { "Id", "ParentId", "StartTime", "EndTime", "Report", "ClassName", "MethodName", "FullName", "Duration", "ThreadId", "ParentThreadId", "NestingLevel", "IsStatic", "IsGeneric", "IsExtension" };
        var parameterHeaders = _reportItems.SelectMany(r => r.Parameters.Keys).Distinct();
        return baseHeaders.Concat(parameterHeaders).ToArray();
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
}

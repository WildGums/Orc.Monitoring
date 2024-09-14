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
        _csvUtils.WriteCsvLine(_writer, headers.Select(EscapeCsvContent).ToArray());

        foreach (var item in PrepareReportItems())
        {
            var values = headers.Select(h => EscapeCsvContent(item.GetValueOrDefault(h, string.Empty))).ToArray();
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
        if (content.Contains(',') || content.Contains('"') || content.Contains('\n') || content.Contains('\r'))
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
            var values = headers.Select(h => EscapeCsvContent(item.GetValueOrDefault(h))).ToArray();

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

    public async Task WriteRelationshipsCsvAsync()
    {
        var headers = new[] { "From", "To", "RelationType" };
        await _csvUtils.WriteCsvLineAsync(_writer, headers);

        var relationships = _reportItems
            .Where(r => !string.IsNullOrEmpty(r.Parent))
            .Select(item => new
            {
                From = item.Parent,
                To = item.Id,
                RelationType = DetermineRelationType(item)
            });

        var counter = 0;
        foreach (var relationship in relationships)
        {
            await _csvUtils.WriteCsvLineAsync(_writer, [relationship.From, relationship.To, relationship.RelationType]);
            counter++;
        }

        _logger.LogInformation($"Wrote {counter} relationships to CSV asynchronously");
    }

    private IEnumerable<Dictionary<string, string>> PrepareReportItems()
    {
        return _reportItems.Select(PrepareReportItem);
    }

    private Dictionary<string, string> PrepareReportItem(ReportItem item)
    {
        var result = new Dictionary<string, string>
        {
            ["Id"] = item.Id ?? string.Empty,
            ["ParentId"] = item.Parent ?? string.Empty,
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

        foreach (var kvp in item.Parameters)
        {
            if (!item.AttributeParameters.Contains(kvp.Key))
            {
                continue;
            }

            result[kvp.Key] = overrides.TryGetValue(kvp.Key, out var overrideValue) ? overrideValue : kvp.Value;
        }

        return result;
    }

    private string[] GetReportItemHeaders()
    {
        var baseHeaders = new[] { "Id", "ParentId", "StartTime", "EndTime", "Report", "ClassName", "MethodName", "FullName", "Duration", "ThreadId", "ParentThreadId", "NestingLevel", "IsStatic", "IsGeneric", "IsExtension" };
        var staticParameter = _reportItems.SelectMany(r => r.AttributeParameters).Distinct();
        var dynamicParameter = _reportItems.SelectMany(r => r.Parameters.Keys.Where(k => !r.AttributeParameters.Contains(k))).Distinct();
        return baseHeaders.Concat(staticParameter).Except(dynamicParameter).ToArray();
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

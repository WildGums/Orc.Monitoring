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

    public CsvReportWriter(
        TextWriter writer,
        IEnumerable<ReportItem> reportItems,
        MethodOverrideManager methodOverrideManager)
        : this(
            writer,
            reportItems,
            methodOverrideManager,
            MonitoringLoggerFactory.Instance,
            CsvUtils.Instance)
    {
    }

    public CsvReportWriter(
        TextWriter writer,
        IEnumerable<ReportItem> reportItems,
        MethodOverrideManager overrideManager,
        IMonitoringLoggerFactory loggerFactory,
        CsvUtils csvUtils)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(reportItems);
        ArgumentNullException.ThrowIfNull(overrideManager);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(csvUtils);

        _writer = writer;
        _reportItems = reportItems;
        _overrideManager = overrideManager;
        _logger = loggerFactory.CreateLogger<CsvReportWriter>();
        _csvUtils = csvUtils;
    }

    public void WriteReportItemsCsv()
    {
        var headers = GetReportItemHeaders();
        var escapedHeaders = headers.Select(EscapeCsvContent).ToArray();
        _csvUtils.WriteCsvLine(_writer, escapedHeaders);

        Func<Dictionary<string, string>, string[]> selector = item =>
            headers.Select(h => EscapeCsvContent(item.GetValueOrDefault(h, string.Empty))).ToArray();

        var items = PrepareReportItems().ToList();
        int itemCount = items.Count;
        for (int i = 0; i < itemCount; i++)
        {
            var item = items[i];
            var values = selector(item);

            if (i < itemCount - 1)
            {
                // Write lines with newline character
                _csvUtils.WriteCsvLine(_writer, values);
            }
            else
            {
                // Write last line without newline character
                string line = string.Join(",", values);
                _writer.Write(line);
            }
        }

        _logger.LogInformation($"Wrote {itemCount} report items to CSV");
    }


    public async Task WriteReportItemsCsvAsync()
    {
        try
        {
            var headers = GetReportItemHeaders();
            var escapedHeaders = headers.Select(EscapeCsvContent).ToArray();
            await _csvUtils.WriteCsvLineAsync(_writer, escapedHeaders);

            Func<Dictionary<string, string>, string[]> selector = item =>
                headers.Select(h => EscapeCsvContent(item.GetValueOrDefault(h, string.Empty))).ToArray();

            var items = PrepareReportItems().ToList();
            int itemCount = items.Count;
            for (int i = 0; i < itemCount; i++)
            {
                var item = items[i];
                var values = selector(item);

                if (i < itemCount - 1)
                {
                    // Write lines with newline character
                    await _csvUtils.WriteCsvLineAsync(_writer, values);
                }
                else
                {
                    // Write last line without newline character
                    string line = string.Join(",", values);
                    await _writer.WriteAsync(line);
                }
            }

            _logger.LogInformation($"Wrote {itemCount} report items to CSV asynchronously");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while writing report items to CSV asynchronously.");
            throw;
        }
    }

    public async Task WriteRelationshipsCsvAsync()
    {
        try
        {
            var headers = new[] { "From", "To", "RelationType" };
            await _csvUtils.WriteCsvLineAsync(_writer, headers);

            var relationships = _reportItems
                .Where(r => !string.IsNullOrEmpty(r.Parent))
                .Select(item => new
                {
                    From = item.Parent ?? string.Empty,
                    To = item.Id ?? string.Empty,
                    RelationType = DetermineRelationType(item)
                });

            var counter = 0;
            foreach (var relationship in relationships)
            {
                await _csvUtils.WriteCsvLineAsync(_writer, new[] { relationship.From, relationship.To, relationship.RelationType });
                counter++;
            }

            _logger.LogInformation($"Wrote {counter} relationships to CSV asynchronously");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while writing relationships to CSV asynchronously.");
            throw;
        }
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
            ["IsStatic"] = GetBooleanPropertyOrDefault(item, "IsStatic", false).ToString(),
            ["IsGeneric"] = GetBooleanPropertyOrDefault(item, "IsGeneric", false).ToString(),
            ["IsExtension"] = GetBooleanPropertyOrDefault(item, "IsExtension", false).ToString()
        };

        var fullName = item.FullName ?? string.Empty;
        var overrides = _overrideManager.GetOverridesForMethod(fullName, item.IsStaticParameter);

        foreach (var kvp in item.Parameters)
        {
            if (item.IsStaticParameter(kvp.Key))
            {
                result[kvp.Key] = overrides.TryGetValue(kvp.Key, out var overrideValue) ? overrideValue : kvp.Value;
            }
            else
            {
                result[kvp.Key] = kvp.Value;
            }
        }

        return result;
    }

    private string[] GetReportItemHeaders()
    {
        var baseHeaders = new[]
        {
            "Id",
            "ParentId",
            "StartTime",
            "EndTime",
            "Report",
            "ClassName",
            "MethodName",
            "FullName",
            "Duration",
            "ThreadId",
            "ParentThreadId",
            "NestingLevel",
            "IsStatic",
            "IsGeneric",
            "IsExtension"
        };

        // Include all attribute parameters across all report items
        var attributeParameters = _reportItems
            .SelectMany(r => r.AttributeParameters)
            .Distinct();

        // Exclude dynamic parameters that are not in attribute parameters
        var dynamicParameters = _reportItems
            .SelectMany(r => r.Parameters.Keys)
            .Where(k => !_reportItems.Any(r => r.AttributeParameters.Contains(k)))
            .Distinct();

        var headers = baseHeaders
            .Concat(attributeParameters)
            .Except(dynamicParameters)
            .ToArray();

        return headers;
    }

    private string DetermineRelationType(ReportItem item)
    {
        if (GetBooleanPropertyOrDefault(item, "IsStatic", false))
            return "Static";
        if (GetBooleanPropertyOrDefault(item, "IsExtension", false))
            return "Extension";
        if (GetBooleanPropertyOrDefault(item, "IsGeneric", false))
            return "Generic";
        return "Regular";
    }

    private static bool GetBooleanPropertyOrDefault(ReportItem item, string propertyName, bool defaultValue)
    {
        if (item.Parameters.TryGetValue(propertyName, out var value) && bool.TryParse(value, out var result))
        {
            return result;
        }
        return defaultValue;
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
}

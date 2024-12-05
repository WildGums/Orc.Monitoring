namespace Orc.Monitoring.Reporters.ReportOutputs;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

public class CsvReportWriter
{
    private readonly string _fileName;
    private readonly IEnumerable<ReportItem> _reportItems;
    private readonly MethodOverrideManager _overrideManager;
    private readonly ILogger<CsvReportWriter> _logger;
    private readonly CsvUtils _csvUtils;

    //public CsvReportWriter(
    //    string fileName,
    //    IEnumerable<ReportItem> reportItems,
    //    MethodOverrideManager methodOverrideManager)
    //    : this(
    //        fileName,
    //        reportItems,
    //        methodOverrideManager,
    //        MonitoringLoggerFactory.Instance,
    //        CsvUtils.Instance)
    //{
    //}

    public CsvReportWriter(
        string fileName,
        IEnumerable<ReportItem> reportItems,
        MethodOverrideManager overrideManager,
        IMonitoringLoggerFactory loggerFactory,
        CsvUtils csvUtils)
    {
        ArgumentNullException.ThrowIfNull(reportItems);
        ArgumentNullException.ThrowIfNull(overrideManager);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(csvUtils);

        _fileName = fileName;
        _reportItems = reportItems;
        _overrideManager = overrideManager;
        _logger = loggerFactory.CreateLogger<CsvReportWriter>();
        _csvUtils = csvUtils;
    }

    public async Task WriteReportItemsCsvAsync()
    {
        try
        {
            var headers = GetReportItemHeaders();

            var items = PrepareReportItems(headers).ToList();

            await _csvUtils.WriteCsvAsync(_fileName, items, headers);

            _logger.LogDebug($"Wrote {items.Count} report items to CSV asynchronously");
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
            var headers = GetRelationshipHeaders();
            
            var relationships = _reportItems
                .Where(r => !string.IsNullOrEmpty(r.Parent))
                .Select(item => new Dictionary<string, string>
                {
                    {"From", item.Parent ?? string.Empty },
                    {"To", item.Id ?? string.Empty },
                    {"RelationType", DetermineRelationType(item) }
                }).ToArray();

            await _csvUtils.WriteCsvAsync(_fileName, relationships, headers);

            _logger.LogInformation($"Wrote {relationships.Length} relationships to CSV asynchronously");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while writing relationships to CSV asynchronously.");
            throw;
        }
    }

    private IEnumerable<Dictionary<string, string>> PrepareReportItems(string[] headers)
    {
        return _reportItems.Select(item => PrepareReportItem(item, headers));
    }

    private Dictionary<string, string> PrepareReportItem(ReportItem item, string[] headers)
    {
        var result = new Dictionary<string, string>();

        var attributeParameters = item.AttributeParameters;
        var dynamicParameters = item.Parameters;

        foreach (var header in headers)
        {
            switch (header)
            {
                case "Id":
                    result["Id"] = item.Id ?? string.Empty;
                    break;

                case "ParentId":
                    result["ParentId"] = item.Parent ?? string.Empty;
                    break;

                case "StartTime":
                    result["StartTime"] = item.StartTime ?? string.Empty;
                    break;

                case "EndTime":
                    result["EndTime"] = item.EndTime ?? string.Empty;
                    break;

                case "Report":
                    result["Report"] = item.Report ?? string.Empty;
                    break;

                case "ClassName":
                    result["ClassName"] = item.ClassName ?? string.Empty;
                    break;

                case "MethodName":
                    result["MethodName"] = item.MethodName ?? string.Empty;
                    break;

                case "FullName":
                    result["FullName"] = item.FullName ?? string.Empty;
                    break;

                case "Duration":
                    result["Duration"] = item.Duration ?? string.Empty;
                    break;

                case "ThreadId":
                    result["ThreadId"] = item.ThreadId ?? string.Empty;
                    break;

                case "ParentThreadId":
                    result["ParentThreadId"] = item.ParentThreadId ?? string.Empty;
                    break;

                case "NestingLevel":
                    result["NestingLevel"] = item.Level ?? string.Empty;
                    break;

                case "IsStatic":
                    result["IsStatic"] = GetBooleanPropertyOrDefault(item, "IsStatic", false).ToString();
                    break;

                case "IsGeneric":
                    result["IsGeneric"] = GetBooleanPropertyOrDefault(item, "IsGeneric", false).ToString();
                    break;

                case "IsExtension":
                    result["IsExtension"] = GetBooleanPropertyOrDefault(item, "IsExtension", false).ToString();
                    break;


                default:
                    if (attributeParameters.Contains(header))
                    {
                        result[header] = item.Parameters.TryGetValue(header, out var value) ? value : string.Empty;
                    }
                    else if (dynamicParameters.ContainsKey(header))
                    {
                        result[header] = item.Parameters.TryGetValue(header, out var value) ? value : string.Empty;
                    }
                    else 
                    {
                        result[header] = string.Empty;
                    }
                    break;
            }
        }

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
            .Distinct().ToArray();

        // Exclude dynamic parameters that are not in attribute parameters
        var dynamicParameters = _reportItems
            .SelectMany(r => r.Parameters.Keys)
            .Distinct();

        // Combine all headers and remove duplicates
        var headers = baseHeaders
            .Concat(attributeParameters)
            .Concat(dynamicParameters)
            .Distinct()
            .ToArray();

        return headers;
    }

    private string[] GetRelationshipHeaders()
    {
        return new[] { "From", "To", "RelationType" };
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
}

namespace Orc.Monitoring.Reporters;

using System;
using Microsoft.Extensions.Logging;
using ReportOutputs;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Core.Abstractions;
using Core.Models;

public class EnhancedDataPostProcessor(IMonitoringLoggerFactory loggerFactory) : IEnhancedDataPostProcessor
{
    private readonly ILogger<EnhancedDataPostProcessor> _logger = loggerFactory.CreateLogger<EnhancedDataPostProcessor>();

    public List<ReportItem> PostProcessData(List<ReportItem> items)
    {
        _logger.LogInformation($"Starting post-processing of {items.Count} items");

        var idMap = items.ToDictionary(i => i.Id ?? string.Empty, i => i);
        var rootItems = new List<ReportItem>();
        var processedItems = new List<ReportItem>();

        foreach (var item in items)
        {
            _logger.LogDebug($"Examining item: Id={item.Id}, MethodName={item.MethodName}, Parent={item.Parent}, IsRoot={item.IsRoot}");
            if (item.Parent is null || item.IsRoot)
            {
                rootItems.Add(item);
                _logger.LogDebug($"Added root item: Id={item.Id}, MethodName={item.MethodName}");
            }
        }

        if (rootItems.Count > 1)
        {
            _logger.LogWarning($"Found {rootItems.Count} root items. Expected only one. Using the first one.");
        }

        foreach (var rootItem in rootItems)
        {
            ProcessItem(rootItem, idMap, processedItems);
        }

        var rootId = rootItems.FirstOrDefault()?.Id;
        ReplaceRootId(processedItems, rootId, "ROOT");

        _logger.LogInformation($"Post-processing completed. Result contains {processedItems.Count} items");
        return processedItems;
    }

    private static void ReplaceRootId(List<ReportItem> processedItems, string? rootId, string newRootId)
    {
        foreach (var item in processedItems)
        {
            if (item.Parent == rootId)
            {
                item.Parent = newRootId;
            }

            if(item.Id == rootId)
            {
                item.Id = newRootId;
            }
        }
    }

    private void ProcessItem(ReportItem item, Dictionary<string, ReportItem> idMap, List<ReportItem> processedItems)
    {
        _logger.LogDebug($"Processing item: Id={item.Id}, MethodName={item.MethodName}, Parent={item.Parent}");
        processedItems.Add(item);

        var childItems = idMap.Values
            .Where(i => i.Parent == item.Id)
            .OrderBy(i => DateTime.Parse(i.StartTime ?? DateTime.MinValue.ToString(CultureInfo.InvariantCulture)))
            .ToList();

        _logger.LogDebug($"Found {childItems.Count} children for item {item.Id}");

        foreach (var childItem in childItems)
        {
            ProcessItem(childItem, idMap, processedItems);
        }
    }
}

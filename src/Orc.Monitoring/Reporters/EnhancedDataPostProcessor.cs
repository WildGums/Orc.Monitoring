namespace Orc.Monitoring.Reporters;

using System;
using Microsoft.Extensions.Logging;
using Orc.Monitoring.Reporters.ReportOutputs;
using System.Collections.Generic;
using System.Linq;

public class EnhancedDataPostProcessor : IEnhancedDataPostProcessor
{
    private readonly ILogger<EnhancedDataPostProcessor> _logger;

    public EnhancedDataPostProcessor(IMonitoringLoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<EnhancedDataPostProcessor>();
    }

    public List<ReportItem> PostProcessData(List<ReportItem> items)
    {
        _logger.LogInformation($"Starting post-processing of {items.Count} items");

        var idMap = items.ToDictionary(i => i.Id ?? string.Empty, i => i);
        var rootItems = new List<ReportItem>();
        var processedItems = new List<ReportItem>();

        foreach (var item in items)
        {
            if (item.Parent is null || item.Parent == "ROOT")
            {
                rootItems.Add(item);
            }
        }

        foreach (var rootItem in rootItems)
        {
            ProcessItem(rootItem, idMap, processedItems);
        }

        _logger.LogInformation($"Post-processing completed. Result contains {processedItems.Count} items");
        return processedItems;
    }

    private void ProcessItem(ReportItem item, Dictionary<string, ReportItem> idMap, List<ReportItem> processedItems)
    {
        processedItems.Add(item);
        _logger.LogDebug($"Processing item: {item.Id} (Parent: {item.Parent})");

        var childItems = idMap.Values
            .Where(i => i.Parent == item.Id)
            .OrderBy(i => DateTime.Parse(i.StartTime ?? DateTime.MinValue.ToString()))
            .ToList();

        foreach (var childItem in childItems)
        {
            ProcessItem(childItem, idMap, processedItems);
        }
    }
}

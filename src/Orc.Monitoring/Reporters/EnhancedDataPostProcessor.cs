namespace Orc.Monitoring.Reporters;

using System;
using Microsoft.Extensions.Logging;
using Orc.Monitoring.Reporters.ReportOutputs;
using System.Collections.Generic;
using System.Linq;

public class EnhancedDataPostProcessor : IEnhancedDataPostProcessor
{
    private readonly ILogger<EnhancedDataPostProcessor> _logger;

    public EnhancedDataPostProcessor(ILogger<EnhancedDataPostProcessor> logger)
    {
        _logger = logger;
    }

    public List<ReportItem> PostProcessData(List<ReportItem> items, OrphanedNodeStrategy strategy)
    {
        _logger.LogInformation($"Starting post-processing of {items.Count} items with strategy: {strategy}");

        var idSet = new HashSet<string>(items.Select(i => i.Id ?? string.Empty));
        var result = new List<ReportItem>();
        var orphanedNodes = new List<ReportItem>();

        // Ensure ROOT node exists
        var rootNode = items.FirstOrDefault(i => i.Id == "ROOT") ?? new ReportItem
        {
            Id = "ROOT",
            MethodName = "Root",
            StartTime = items.Min(r => DateTime.Parse(r.StartTime ?? DateTime.MinValue.ToString())).ToString("yyyy-MM-dd HH:mm:ss.fff"),
            EndTime = items.Max(r => DateTime.Parse(r.EndTime ?? DateTime.MaxValue.ToString())).ToString("yyyy-MM-dd HH:mm:ss.fff"),
            Parent = null
        };
        result.Add(rootNode);
        idSet.Add("ROOT");

        foreach (var item in items.Where(i => i.Id != "ROOT"))
        {
            var itemParent = item.Parent ?? string.Empty;
            if (itemParent == "ROOT" || idSet.Contains(itemParent))
            {
                result.Add(item);
                _logger.LogDebug($"Item {item.Id} (Parent: {itemParent}) added to result");
            }
            else
            {
                orphanedNodes.Add(item);
                _logger.LogWarning($"Orphaned node found: {item.Id} (Parent: {itemParent})");
            }
        }

        ProcessOrphanedNodes(orphanedNodes, result, idSet, strategy);

        // Sort items to maintain correct order
        result = result.OrderBy(i => i.Id == "ROOT" ? 0 : 1)
            .ThenBy(i => DateTime.Parse(i.StartTime ?? DateTime.MinValue.ToString()))
            .ToList();

        _logger.LogInformation($"Post-processing completed. Result contains {result.Count} items");

        return result;
    }

    private void ProcessOrphanedNodes(List<ReportItem> orphanedNodes, List<ReportItem> result, HashSet<string> idSet, OrphanedNodeStrategy strategy)
    {
        _logger.LogInformation($"Processing {orphanedNodes.Count} orphaned nodes with strategy: {strategy}");

        switch (strategy)
        {
            case OrphanedNodeStrategy.RemoveOrphans:
                _logger.LogInformation($"Removing {orphanedNodes.Count} orphaned nodes");
                break;

            case OrphanedNodeStrategy.AttachToRoot:
                _logger.LogInformation($"Attaching {orphanedNodes.Count} orphaned nodes to ROOT");
                foreach (var orphan in orphanedNodes)
                {
                    orphan.Parent = "ROOT";
                    result.Add(orphan);
                    _logger.LogDebug($"Orphan {orphan.Id} attached to ROOT");
                }
                break;

            case OrphanedNodeStrategy.AttachToNearestAncestor:
                _logger.LogInformation($"Attaching {orphanedNodes.Count} orphaned nodes to nearest ancestors");
                foreach (var orphan in orphanedNodes)
                {
                    var nearestAncestor = FindNearestAncestor(orphan, result, idSet);
                    orphan.Parent = nearestAncestor?.Id ?? "ROOT";
                    result.Add(orphan);
                    idSet.Add(orphan.Id ?? string.Empty);
                    _logger.LogDebug($"Orphan {orphan.Id} attached to {orphan.Parent}");
                }
                break;
        }

        _logger.LogInformation($"Processed orphaned nodes. Result now contains {result.Count} items");
    }

    private ReportItem? FindNearestAncestor(ReportItem item, List<ReportItem> allItems, HashSet<string> validIds)
    {
        _logger.LogDebug($"Finding nearest ancestor for item {item.Id} (Parent: {item.Parent})");

        var currentParentId = item.Parent;
        var visitedIds = new HashSet<string>();

        while (currentParentId is not null && currentParentId != "ROOT")
        {
            if (visitedIds.Contains(currentParentId))
            {
                _logger.LogWarning($"Circular reference detected for item {item.Id}. Breaking the loop.");
                break;
            }

            visitedIds.Add(currentParentId);

            if (validIds.Contains(currentParentId))
            {
                var ancestor = allItems.First(i => i.Id == currentParentId);
                _logger.LogDebug($"Found valid ancestor {ancestor.Id} for item {item.Id}");
                return ancestor;
            }

            var nextParent = allItems.FirstOrDefault(i => i.Id == currentParentId);
            if (nextParent is null)
            {
                _logger.LogWarning($"Parent {currentParentId} not found in allItems for item {item.Id}");
                break;
            }
            currentParentId = nextParent.Parent;
        }

        // If no valid ancestor found, return the first non-ROOT item
        var firstNonRootItem = allItems.FirstOrDefault(i => i.Id != "ROOT" && i.Id != item.Id);
        _logger.LogWarning($"No valid ancestor found for item {item.Id}. Returning {firstNonRootItem?.Id ?? "null"}.");
        return firstNonRootItem ?? allItems.First(i => i.Id == "ROOT");
    }
}

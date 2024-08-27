namespace Orc.Monitoring.Reporters;

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Orc.Monitoring.Reporters.ReportOutputs;

public class EnhancedDataPostProcessor
{
    private readonly ILogger<EnhancedDataPostProcessor> _logger;

    public enum OrphanedNodeStrategy
    {
        RemoveOrphans,
        AttachToRoot,
        AttachToNearestAncestor
    }

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

        foreach (var item in items)
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

        EnsureRootNodeExists(result);

        _logger.LogInformation($"Post-processing completed. Result contains {result.Count} items");

        return result;
    }

    private void ProcessOrphanedNodes(List<ReportItem> orphanedNodes, List<ReportItem> result, HashSet<string> idSet, OrphanedNodeStrategy strategy)
    {
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
                }
                break;

            case OrphanedNodeStrategy.AttachToNearestAncestor:
                _logger.LogInformation($"Attaching {orphanedNodes.Count} orphaned nodes to nearest ancestors");
                foreach (var orphan in orphanedNodes)
                {
                    var nearestAncestor = FindNearestAncestor(orphan, result, idSet);
                    if (nearestAncestor is not null)
                    {
                        orphan.Parent = nearestAncestor.Id;
                        result.Add(orphan);
                        _logger.LogDebug($"Orphan {orphan.Id} attached to ancestor {nearestAncestor.Id}");
                    }
                    else
                    {
                        orphan.Parent = "ROOT";
                        result.Add(orphan);
                        _logger.LogDebug($"Orphan {orphan.Id} attached to ROOT as no valid ancestor found");
                    }
                }
                break;
        }
    }

    private ReportItem? FindNearestAncestor(ReportItem item, List<ReportItem> allItems, HashSet<string> validIds)
    {
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
                return allItems.First(i => i.Id == currentParentId);
            }

            var nextParent = allItems.FirstOrDefault(i => i.Id == currentParentId);
            currentParentId = nextParent?.Parent;
        }

        return null;
    }

    private void EnsureRootNodeExists(List<ReportItem> items)
    {
        if (!items.Any(i => i.Id == "ROOT"))
        {
            var rootNode = new ReportItem
            {
                Id = "ROOT",
                MethodName = "Root",
                StartTime = items.Min(r => DateTime.Parse(r.StartTime ?? DateTime.MinValue.ToString())).ToString("yyyy-MM-dd HH:mm:ss.fff"),
                EndTime = items.Max(r => DateTime.Parse(r.EndTime ?? DateTime.MaxValue.ToString())).ToString("yyyy-MM-dd HH:mm:ss.fff"),
                Parent = null
            };
            items.Insert(0, rootNode);
            _logger.LogInformation("Added synthetic ROOT node");
        }
    }
}

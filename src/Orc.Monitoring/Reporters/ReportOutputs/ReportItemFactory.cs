namespace Orc.Monitoring.Reporters.ReportOutputs;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MethodLifeCycleItems;
using Microsoft.Extensions.Logging;

public class ReportItemFactory : IReportItemFactory
{
    private readonly ILogger<ReportItemFactory> _logger;

    public ReportItemFactory(IMonitoringLoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        
        _logger = loggerFactory.CreateLogger<ReportItemFactory>();
    }

    public ReportItem CloneReportItemWithOverrides(ReportItem item, MethodOverrideManager methodOverrideManager)
    {
        var fullName = item.Parameters.TryGetValue("FullName", out var fn) ? fn : item.FullName ?? string.Empty;
        var overrides = methodOverrideManager.GetOverridesForMethod(fullName, item.IsStaticParameter);
        _logger.LogInformation($"Applying overrides for {fullName}: {string.Join(", ", overrides.Select(x => $"{x.Key}={x.Value}"))}");
        var newParameters = new Dictionary<string, string>(item.Parameters, StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in overrides)
        {
            if (item.IsStaticParameter(kvp.Key))
            {
                newParameters[kvp.Key] = kvp.Value;
                _logger.LogInformation($"Applied override: {kvp.Key}={kvp.Value}");
            }
        }

        return new ReportItem
        {
            Id = item.Id,
            StartTime = item.StartTime,
            ItemName = item.ItemName,
            EndTime = item.EndTime,
            Duration = item.Duration,
            Report = item.Report,
            ThreadId = item.ThreadId,
            Level = item.Level,
            ClassName = item.ClassName,
            MethodName = item.MethodName,
            FullName = fullName,
            Parent = item.Parent,
            ParentThreadId = item.ParentThreadId,
            Parameters = newParameters,
            AttributeParameters = new HashSet<string>(item.AttributeParameters)
        };
    }

    public ReportItem CreateReportItem(IMethodLifeCycleItem lifeCycleItem, IMethodCallReporter? reporter)
    {
        var methodCallInfo = lifeCycleItem.MethodCallInfo;
        var id = methodCallInfo.Id;

        id ??= Guid.NewGuid().ToString();

        var reportItem = new ReportItem
        {
            Id = id,
            IsRoot = reporter is not null && methodCallInfo.AssociatedReporters.Contains(reporter),
            StartTime = methodCallInfo.StartTime.ToString("yyyy-MM-dd HH:mm:ss.fff"),
            Report = reporter?.FullName ?? string.Empty,
            ThreadId = methodCallInfo.ThreadId.ToString(),
            Level = methodCallInfo.Level.ToString(),
            ClassName = methodCallInfo.ClassType?.Name ?? string.Empty,
            MethodName = methodCallInfo.MethodName,
            ItemName = methodCallInfo.MethodName,
            FullName = $"{methodCallInfo.ClassType?.Name}.{methodCallInfo.MethodName}",
            Parent = methodCallInfo.Parent?.Id,
            ParentThreadId = methodCallInfo.ParentThreadId.ToString(),
            Parameters = methodCallInfo.Parameters ?? new Dictionary<string, string>(),
            AttributeParameters = methodCallInfo.AttributeParameters ?? []
        };

        _logger.LogInformation($"Created new ReportItem: Id={reportItem.Id}, MethodName={reportItem.MethodName}, Parent={reportItem.Parent}");

        return reportItem;
    }

    public ReportItem UpdateReportItemEnding(MethodCallEnd end, IMethodCallReporter? reporter, Dictionary<string, ReportItem> existingReportItems)
    {
        var methodCallInfo = end.MethodCallInfo;

        var id = methodCallInfo.Id;

        if (string.IsNullOrEmpty(id))
        {
            _logger.LogWarning("Method call ID is null or empty. Attempting to find matching item by method name.");
            var matchingItem = existingReportItems.Values.FirstOrDefault(item => item.MethodName == methodCallInfo.MethodName);
            if (matchingItem is not null)
            {
                id = matchingItem.Id;
                _logger.LogInformation($"Found matching item with Id: {id}");
            }
            else
            {
                _logger.LogWarning("No matching item found. Creating a new item.");
                id = Guid.NewGuid().ToString();
            }
        }

        var reportItem = existingReportItems.GetValueOrDefault(id);
        if (reportItem is null)
        {
            _logger.LogInformation($"Creating new report item for method call ID {id}.");
            reportItem = CreateReportItem(end, reporter);
            
            id = reportItem.Id;
            existingReportItems.Add(id, reportItem);
        }

        var endTime = end.TimeStamp;

        reportItem.EndTime = endTime.ToString("yyyy-MM-dd HH:mm:ss.fff");
        reportItem.Duration = (endTime - DateTime.Parse(reportItem.StartTime ?? string.Empty)).TotalMilliseconds.ToString("N1", CultureInfo.InvariantCulture);
        reportItem.Parameters = end.MethodCallInfo.Parameters ?? new Dictionary<string, string>();

        _logger.LogDebug($"Updated report item: {reportItem.MethodName}, Id: {reportItem.Id}, StartTime: {reportItem.StartTime}, EndTime: {reportItem.EndTime}, Duration: {reportItem.Duration}ms");

        return reportItem;
    }

    public ReportItem CreateGapReportItem(CallGap gap, IMethodCallReporter? reporter)
    {
        var reportItem = new ReportItem
        {
            Id = Guid.NewGuid().ToString(),
            StartTime = gap.TimeStamp.ToString("yyyy-MM-dd HH:mm:ss.fff"),
            EndTime = (gap.TimeStamp + gap.Elapsed).ToString("yyyy-MM-dd HH:mm:ss.fff"),
            Duration = gap.Elapsed.TotalMilliseconds.ToString("N1", CultureInfo.InvariantCulture),
            Report = reporter?.FullName ?? string.Empty,
            ThreadId = string.Empty,
            Level = string.Empty,
            ClassName = MethodCallParameter.Types.Gap,
            MethodName = MethodCallParameter.Types.Gap,
            FullName = MethodCallParameter.Types.Gap,
            Parameters = gap.Parameters
        };

        _logger.LogInformation($"Processed gap: Duration: {reportItem.Duration}ms");

        return reportItem;
    }
}

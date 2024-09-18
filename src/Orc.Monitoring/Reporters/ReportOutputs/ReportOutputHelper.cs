namespace Orc.Monitoring.Reporters.ReportOutputs;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MethodLifeCycleItems;
using Reporters;
using Microsoft.Extensions.Logging;

public class ReportOutputHelper(IMonitoringLoggerFactory loggerFactory)
{
    private readonly ILogger<ReportOutputHelper> _logger = loggerFactory.CreateLogger<ReportOutputHelper>();
    private readonly List<ReportItem> _reportItems = [];

    private IMethodCallReporter? _reporter;
    private OutputLimitOptions _limitOptions = OutputLimitOptions.Unlimited;

    public List<ReportItem> Gaps { get; } = [];
    public HashSet<string> ParameterNames { get; } = [];
    public string? LastEndTime { get; private set; }
    public IReadOnlyCollection<ReportItem> ReportItems => _reportItems;
    public IMethodCallReporter? Reporter => _reporter;

    public void Initialize(IMethodCallReporter reporter)
    {
        _reporter = reporter;
        _reportItems.Clear();
        Gaps.Clear();
        ParameterNames.Clear();
        LastEndTime = null;

        _logger.LogInformation($"ReportOutputHelper initialized for reporter: {reporter.GetType().Name}");
    }

    public ReportItem? ProcessCallStackItem(ICallStackItem callStackItem)
    {
        _logger.LogInformation($"ProcessCallStackItem called with {callStackItem.GetType().Name}");

        ReportItem? result = null;
        switch (callStackItem)
        {
            case MethodCallStart start:
                result = ProcessStart(start);
                _logger.LogInformation($"Processed MethodCallStart: {start.MethodCallInfo.MethodName}, Id: {start.MethodCallInfo.Id}, Result: {result.MethodName}");
                break;

            case MethodCallEnd end:
                result = ProcessEnd(end);
                _logger.LogInformation($"Processed MethodCallEnd: {end.MethodCallInfo.MethodName}, Id: {end.MethodCallInfo.Id}, Result: {result.MethodName}");
                break;

            case CallGap gap:
                result = ProcessGap(gap);
                _logger.LogInformation($"Processed CallGap: Duration: {gap.Elapsed}");
                break;
        }

        if (result is not null)
        {
            AddOrUpdateReportItem(result);
            _logger.LogInformation($"Processed {callStackItem.GetType().Name}: {result.MethodName}, Id: {result.Id}");
        }
        else
        {
            _logger.LogWarning($"Failed to process {callStackItem.GetType().Name}");
        }

        _logger.LogInformation($"Current report items count: {_reportItems.Count}");
        return result;
    }

    private void AddOrUpdateReportItem(ReportItem item)
    {
        var existingItem = _reportItems.FirstOrDefault(r => r.Id == item.Id);
        if (existingItem is null)
        {
            _reportItems.Add(item);
            _logger.LogInformation($"Added new ReportItem: {item.MethodName}, Id: {item.Id}");
        }
        else
        {
            // Update existing item
            existingItem.EndTime = item.EndTime;
            existingItem.Duration = item.Duration;
            existingItem.Parameters = item.Parameters;
            existingItem.AttributeParameters = item.AttributeParameters;
            _logger.LogInformation($"Updated existing ReportItem: {item.MethodName}, Id: {item.Id}");
        }
    }

    private ReportItem ProcessStart(MethodCallStart start)
    {
        var methodCallInfo = start.MethodCallInfo;
        var reporter = Reporter;
        var id = methodCallInfo.Id;

        id ??= Guid.NewGuid().ToString();

        var reportItem = new ReportItem
        {
            Id = id,
            IsRoot = reporter is not null && methodCallInfo.AssociatedReporters.Contains(reporter),
            StartTime = methodCallInfo.StartTime.ToString("yyyy-MM-dd HH:mm:ss.fff"),
            Report = _reporter?.FullName ?? string.Empty,
            ThreadId = methodCallInfo.ThreadId.ToString(),
            Level = methodCallInfo.Level.ToString(),
            ClassName = methodCallInfo.ClassType?.Name ?? string.Empty,
            MethodName = methodCallInfo.MethodName,
            ItemName = methodCallInfo.MethodName,
            FullName = $"{methodCallInfo.ClassType?.Name}.{methodCallInfo.MethodName}",
            Parent = methodCallInfo.Parent?.Id,
            ParentThreadId = methodCallInfo.ParentThreadId.ToString(),
            Parameters = methodCallInfo.Parameters ?? new Dictionary<string, string>(),
        };

        _logger.LogInformation($"Created new ReportItem: Id={reportItem.Id}, MethodName={reportItem.MethodName}, Parent={reportItem.Parent}");

        return reportItem;
    }

    private ReportItem ProcessEnd(MethodCallEnd end)
    {
        var methodCallInfo = end.MethodCallInfo;

        var id = methodCallInfo.Id;

        if (string.IsNullOrEmpty(id))
        {
            _logger.LogWarning("Method call ID is null or empty. Attempting to find matching item by method name.");
            var matchingItem = _reportItems.FirstOrDefault(item => item.MethodName == methodCallInfo.MethodName);
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

        var reportItem = _reportItems.FirstOrDefault(item => item.Id == id);
        if (reportItem is null)
        {
            _logger.LogInformation($"Creating new report item for method call ID {id}.");
            reportItem = new ReportItem
            {
                Id = id,
                MethodName = methodCallInfo.MethodName,
                StartTime = methodCallInfo.StartTime.ToString("yyyy-MM-dd HH:mm:ss.fff")
            };
            _reportItems.Add(reportItem);
        }

        var endTime = end.TimeStamp;
        LastEndTime = endTime.ToString("yyyy-MM-dd HH:mm:ss.fff");
        reportItem.EndTime = LastEndTime;
        reportItem.Duration = (endTime - DateTime.Parse(reportItem.StartTime ?? string.Empty)).TotalMilliseconds.ToString("N1", CultureInfo.InvariantCulture);
        reportItem.Parameters = methodCallInfo.Parameters ?? new Dictionary<string, string>();
        reportItem.AttributeParameters = methodCallInfo.AttributeParameters ?? [];

        ParameterNames.UnionWith(reportItem.Parameters.Keys);

        _logger.LogDebug($"Updated report item: {reportItem.MethodName}, Id: {reportItem.Id}, StartTime: {reportItem.StartTime}, EndTime: {reportItem.EndTime}, Duration: {reportItem.Duration}ms");

        return reportItem;
    }

    private ReportItem ProcessGap(CallGap gap)
    {
        var reportItem = new ReportItem
        {
            Id = Guid.NewGuid().ToString(),
            StartTime = gap.TimeStamp.ToString("yyyy-MM-dd HH:mm:ss.fff"),
            EndTime = (gap.TimeStamp + gap.Elapsed).ToString("yyyy-MM-dd HH:mm:ss.fff"),
            Duration = gap.Elapsed.TotalMilliseconds.ToString("N1", CultureInfo.InvariantCulture),
            Report = _reporter?.FullName ?? string.Empty,
            ThreadId = string.Empty,
            Level = string.Empty,
            ClassName = MethodCallParameter.Types.Gap,
            MethodName = MethodCallParameter.Types.Gap,
            FullName = MethodCallParameter.Types.Gap,
            Parameters = gap.Parameters
        };

        ParameterNames.UnionWith(reportItem.Parameters.Keys);
        Gaps.Add(reportItem);

        _logger.LogInformation($"Processed gap: Duration: {reportItem.Duration}ms");

        return reportItem;
    }

    public List<ReportItem> GetReportItems()
    {
        return _reportItems;
    }

    public void AddReportItem(ReportItem item)
    {
        var existingItem = _reportItems.FirstOrDefault(r => r.Id == item.Id);
        if (existingItem is null)
        {
            // Add new item
            _reportItems.Add(item);
            _logger.LogDebug($"Added new ReportItem: Id={item.Id}, MethodName={item.MethodName}");
        }
        else
        {
            // Update existing item
            existingItem.EndTime = item.EndTime;
            existingItem.Duration = item.Duration;
            _logger.LogDebug($"Updated existing ReportItem: Id={item.Id}, MethodName={item.MethodName}");
        }

        _logger.LogDebug($"Added report item. Current count: {_reportItems.Count}");
        ApplyLimits();
    }

    private void ApplyLimits()
    {
        _logger.LogInformation($"Applying limits. Current ReportItems count: {_reportItems.Count}, Gaps count: {Gaps.Count}");

        if (_limitOptions.MaxItems.HasValue)
        {
            var itemsToRemove = _reportItems.Count - _limitOptions.MaxItems.Value;
            if (itemsToRemove > 0)
            {
                var itemsToKeep = _reportItems
                    .OrderByDescending(i => DateTime.Parse(i.StartTime ?? DateTime.MinValue.ToString(CultureInfo.InvariantCulture)))
                    .Take(_limitOptions.MaxItems.Value)
                    .ToList();

                _reportItems.Clear();
                _reportItems.AddRange(itemsToKeep);

                _logger.LogInformation($"Applied limits. Removed {itemsToRemove} items.");
            }
        }

        _logger.LogInformation($"After applying limits - ReportItems count: {_reportItems.Count}, Gaps count: {Gaps.Count}");
    }

    public void SetLimitOptions(OutputLimitOptions options)
    {
        _limitOptions = options;
        ApplyLimits();
        _logger.LogInformation($"Limit options set: MaxItems = {options.MaxItems}");
    }

    public OutputLimitOptions GetLimitOptions()
    {
        return _limitOptions;
    }

    public string GetDebugInfo()
    {
        return $"ReportItems: {_reportItems.Count}, " +
               $"Gaps: {Gaps.Count}, " +
               $"ParameterNames: {string.Join(", ", ParameterNames)}, " +
               $"LastEndTime: {LastEndTime}, " +
               $"LimitOptions: MaxItems={_limitOptions.MaxItems}";
    }
}

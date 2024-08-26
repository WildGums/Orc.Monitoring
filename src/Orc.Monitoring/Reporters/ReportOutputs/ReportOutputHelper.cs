namespace Orc.Monitoring.Reporters.ReportOutputs;

using System;
using System.Collections.Generic;
using System.Linq;
using Orc.Monitoring.MethodLifeCycleItems;
using Orc.Monitoring.Reporters;
using Microsoft.Extensions.Logging;

/// <summary>
/// Provides helper functionality for report outputs, including item processing and limit application.
/// </summary>
public class ReportOutputHelper
{
    private readonly ILogger<ReportOutputHelper> _logger = MonitoringController.CreateLogger<ReportOutputHelper>();

    public IMethodCallReporter? Reporter { get; private set; }
    private readonly List<ReportItem> _reportItems = new();
    public IReadOnlyList<ReportItem> ReportItems => _reportItems.AsReadOnly();
    public List<ReportItem> Gaps { get; } = new();
    private readonly Dictionary<int, Stack<string>> _methodStack = new();
    public HashSet<string> ParameterNames { get; } = new();
    public string? LastEndTime { get; private set; }

    private OutputLimitOptions _limitOptions = OutputLimitOptions.Unlimited;

    /// <summary>
    /// Initializes the helper with the specified reporter.
    /// </summary>
    /// <param name="reporter">The method call reporter to be used.</param>
    public void Initialize(IMethodCallReporter reporter)
    {
        Reporter = reporter;
        _reportItems.Clear();
        _methodStack.Clear();
        Gaps.Clear();
        ParameterNames.Clear();
        LastEndTime = null;

        _logger.LogInformation($"ReportOutputHelper initialized for reporter: {reporter.GetType().Name}");
    }

    /// <summary>
    /// Processes a call stack item and returns a corresponding report item.
    /// </summary>
    /// <param name="callStackItem">The call stack item to process.</param>
    /// <returns>A report item based on the processed call stack item, or null if processing failed.</returns>
    public ReportItem? ProcessCallStackItem(ICallStackItem callStackItem)
    {
        _logger.LogDebug($"Processing CallStackItem: {callStackItem.GetType().Name}");

        ReportItem? result = null;
        switch (callStackItem)
        {
            case MethodCallStart start:
                result = ProcessStart(start);
                _logger.LogDebug($"Processed MethodCallStart: {start.MethodCallInfo.MethodName}, Id: {start.MethodCallInfo.Id}");
                break;
            case MethodCallEnd end:
                result = ProcessEnd(end);
                _logger.LogDebug($"Processed MethodCallEnd: {end.MethodCallInfo.MethodName}, Id: {end.MethodCallInfo.Id}");
                break;
            case CallGap gap:
                result = ProcessGap(gap);
                _logger.LogDebug($"Processed CallGap: Duration: {gap.Elapsed}");
                break;
        }

        if (result is not null && !string.IsNullOrEmpty(result.Id))
        {
            AddReportItem(result);
        }
        else
        {
            _logger.LogWarning($"No valid ReportItem created for CallStackItem. Result is null: {result is null}, Id is null or empty: {result?.Id is null || result?.Id == string.Empty}");
        }

        _logger.LogDebug($"Current ReportItems count: {_reportItems.Count}, Gaps count: {Gaps.Count}");

        ApplyLimits();
        return result;
    }

    /// <summary>
    /// Adds a report item to the collection and applies limits.
    /// </summary>
    /// <param name="item">The report item to add.</param>
    public void AddReportItem(ReportItem item)
    {
        _reportItems.Add(item);
        _logger.LogDebug($"Added report item. Current count: {_reportItems.Count}");
        ApplyLimits();
    }

    private ReportItem ProcessStart(MethodCallStart start)
    {
        var methodCallInfo = start.MethodCallInfo;
        var threadId = methodCallInfo.ThreadId;
        var classTypeName = methodCallInfo.ClassType?.Name ?? string.Empty;
        var methodName = methodCallInfo.MethodName;
        var id = methodCallInfo.Id ?? Guid.NewGuid().ToString();

        var reportItem = new ReportItem
        {
            Id = id,
            StartTime = methodCallInfo.StartTime.ToString("yyyy-MM-dd HH:mm:ss.fff"),
            Report = Reporter?.FullName ?? string.Empty,
            ThreadId = threadId.ToString(),
            Level = methodCallInfo.Level.ToString(),
            ClassName = classTypeName,
            MethodName = methodName,
            ItemName = methodName,
            FullName = $"{classTypeName}.{methodName}",
        };

        if (!_methodStack.TryGetValue(threadId, out var stack))
        {
            stack = new Stack<string>();
            _methodStack[threadId] = stack;
        }

        reportItem.Parent = stack.Count > 0 ? stack.Peek() : "ROOT";
        reportItem.ParentThreadId = methodCallInfo.ParentThreadId.ToString();

        stack.Push(id);

        _logger.LogDebug($"Processed start: {reportItem.FullName}, Id: {reportItem.Id}");

        return reportItem;
    }

    private ReportItem? ProcessEnd(MethodCallEnd end)
    {
        var methodCallInfo = end.MethodCallInfo;
        var threadId = methodCallInfo.ThreadId;
        var key = methodCallInfo.Id ?? string.Empty;

        var reportItem = _reportItems.FirstOrDefault(r => r.Id == key);
        if (reportItem is null)
        {
            _logger.LogWarning($"No report item found for method call ID {key}");
            return null;
        }

        var endTime = methodCallInfo.StartTime + methodCallInfo.Elapsed;
        LastEndTime = endTime.ToString("yyyy-MM-dd HH:mm:ss.fff");
        reportItem.EndTime = LastEndTime;
        reportItem.Duration = methodCallInfo.Elapsed.TotalMilliseconds.ToString("N1");
        reportItem.Parameters = methodCallInfo.Parameters ?? new Dictionary<string, string>();
        reportItem.AttributeParameters = methodCallInfo.AttributeParameters ?? new HashSet<string>();

        ParameterNames.UnionWith(reportItem.Parameters.Keys);

        if (_methodStack.TryGetValue(threadId, out var stack) && stack.Count > 0)
        {
            stack.Pop();
            if (stack.Count == 0)
            {
                _methodStack.Remove(threadId);
            }
        }

        _logger.LogDebug($"Processed end: {reportItem.FullName}, Id: {reportItem.Id}");

        return reportItem;
    }

    private ReportItem ProcessGap(CallGap gap)
    {
        var reportItem = new ReportItem
        {
            Id = Guid.NewGuid().ToString(),
            StartTime = gap.TimeStamp.ToString("yyyy-MM-dd HH:mm:ss.fff"),
            EndTime = (gap.TimeStamp + gap.Elapsed).ToString("yyyy-MM-dd HH:mm:ss.fff"),
            Duration = gap.Elapsed.TotalMilliseconds.ToString("N1"),
            Report = Reporter?.FullName ?? string.Empty,
            ThreadId = string.Empty,
            Level = string.Empty,
            ClassName = MethodCallParameter.Types.Gap,
            MethodName = MethodCallParameter.Types.Gap,
            FullName = MethodCallParameter.Types.Gap,
            Parameters = gap.Parameters
        };

        ParameterNames.UnionWith(reportItem.Parameters.Keys);
        Gaps.Add(reportItem);

        _logger.LogDebug($"Processed gap: Duration = {reportItem.Duration}ms");

        return reportItem;
    }

    /// <summary>
    /// Applies the current limit options to the report items and gaps.
    /// </summary>
    private void ApplyLimits()
    {
        if (_limitOptions.MaxItems.HasValue)
        {
            int totalItems = _reportItems.Count + Gaps.Count;
            int itemsToRemove = totalItems - _limitOptions.MaxItems.Value;

            if (itemsToRemove > 0)
            {
                var allItems = _reportItems.Concat(Gaps).OrderBy(i => i.StartTime).ToList();
                var itemsToKeep = allItems.Skip(itemsToRemove).ToList();

                _reportItems.Clear();
                _reportItems.AddRange(itemsToKeep.Where(i => !Gaps.Contains(i)));

                Gaps.Clear();
                Gaps.AddRange(itemsToKeep.Where(i => !_reportItems.Contains(i)));

                _logger.LogInformation($"Applied limits. Removed {itemsToRemove} items.");
            }
        }

        _logger.LogInformation($"After applying limits - ReportItems count: {_reportItems.Count}, Gaps count: {Gaps.Count}");
    }

    /// <summary>
    /// Sets the limit options for the helper.
    /// </summary>
    /// <param name="options">The output limit options to set.</param>
    public void SetLimitOptions(OutputLimitOptions options)
    {
        _limitOptions = options;
        ApplyLimits();
        _logger.LogInformation($"Limit options set: MaxItems = {options.MaxItems}");
    }

    /// <summary>
    /// Gets the current limit options for the helper.
    /// </summary>
    /// <returns>The current output limit options.</returns>
    public OutputLimitOptions GetLimitOptions()
    {
        return _limitOptions;
    }

    /// <summary>
    /// Gets debug information about the current state of the helper.
    /// </summary>
    /// <returns>A string containing debug information.</returns>
    public string GetDebugInfo()
    {
        return $"ReportItems: {_reportItems.Count}, " +
               $"Gaps: {Gaps.Count}, " +
               $"ParameterNames: {string.Join(", ", ParameterNames)}, " +
               $"LastEndTime: {LastEndTime}, " +
               $"LimitOptions: MaxItems={_limitOptions.MaxItems}";
    }
}

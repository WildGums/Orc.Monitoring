namespace Orc.Monitoring.Reporters.ReportOutputs;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Orc.Monitoring.MethodLifeCycleItems;
using Orc.Monitoring.Reporters;
using Microsoft.Extensions.Logging;

public class ReportOutputHelper
{
    private readonly ILogger<ReportOutputHelper> _logger = MonitoringController.CreateLogger<ReportOutputHelper>();

    public IMethodCallReporter? Reporter { get; private set; }
    private readonly ConcurrentDictionary<string, ReportItem> _reportItems = new();
    public ICollection<ReportItem> ReportItems => _reportItems.Values;
    public List<ReportItem> Gaps { get; } = new();
    private readonly ConcurrentDictionary<int, Stack<string>> _methodStack = new();
    public HashSet<string> ParameterNames { get; } = new();
    public string? LastEndTime { get; private set; }

    private OutputLimitOptions _limitOptions = OutputLimitOptions.Unlimited;

    public void Initialize(IMethodCallReporter reporter)
    {
        Reporter = reporter;
        _reportItems.Clear();
        _methodStack.Clear();
        Gaps.Clear();
        ParameterNames.Clear();
        LastEndTime = null;

        if (reporter is ILimitedOutput limitedOutput)
        {
            _limitOptions = limitedOutput.GetLimitOptions();
        }
    }

    public ReportItem? ProcessCallStackItem(ICallStackItem callStackItem)
    {
        ReportItem? result = null;
        switch (callStackItem)
        {
            case MethodCallStart start:
                result = ProcessStart(start);
                break;
            case MethodCallEnd end:
                result = ProcessEnd(end);
                break;
            case CallGap gap:
                result = ProcessGap(gap);
                break;
        }

        ApplyLimits();
        return result;
    }

    private ReportItem ProcessStart(MethodCallStart start)
    {
        if (Reporter is null)
        {
            throw new InvalidOperationException("Reporter is not initialized");
        }

        var methodCallInfo = start.MethodCallInfo;
        var threadId = methodCallInfo.ThreadId;
        var classTypeName = methodCallInfo.ClassType?.Name ?? string.Empty;
        var methodName = methodCallInfo.MethodName;
        var id = methodCallInfo.Id ?? string.Empty;

        var reportItem = new ReportItem
        {
            Id = id,
            StartTime = methodCallInfo.StartTime.ToString("yyyy-MM-dd HH:mm:ss.fff"),
            Report = Reporter.FullName,
            ThreadId = threadId.ToString(),
            Level = methodCallInfo.Level.ToString(),
            ClassName = classTypeName,
            MethodName = methodCallInfo.MethodInfo?.Name ?? methodName,
            FullName = $"{classTypeName}.{methodName}",
        };

        _reportItems.TryAdd(id, reportItem);

        if (!_methodStack.TryGetValue(threadId, out var stack))
        {
            stack = new Stack<string>();
            _methodStack.TryAdd(threadId, stack);
        }

        reportItem.Parent = methodCallInfo.Parent?.Id;
        reportItem.ParentThreadId = methodCallInfo.ParentThreadId.ToString();

        stack.Push(id);

        return reportItem;
    }

    private ReportItem? ProcessEnd(MethodCallEnd end)
    {
        if (Reporter is null)
        {
            _logger.LogWarning("Reporter is null when processing end event.");
            return null;
        }

        var methodCallInfo = end.MethodCallInfo;
        var threadId = methodCallInfo.ThreadId;
        var key = methodCallInfo.Id ?? string.Empty;

        if (!_reportItems.TryGetValue(key, out var reportItem))
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

        if (!_methodStack.TryGetValue(threadId, out var stack))
        {
            _logger.LogWarning($"No stack found for thread {threadId} when processing end event.");
            return null;
        }

        if (stack.Count == 0)
        {
            _logger.LogWarning($"Attempted to process end event for thread {threadId} with an empty stack.");
            return null;
        }

        var poppedId = stack.Pop();
        if (poppedId != key)
        {
            _logger.LogWarning($"Mismatch in method call IDs. Expected {key}, but popped {poppedId}.");
        }

        if (stack.Count == 0)
        {
            _methodStack.TryRemove(threadId, out _);
        }

        var remainingStacksInfo = string.Join(", ", _methodStack.Select(kvp => $"Thread {kvp.Key}: {kvp.Value.Count} items"));
        _logger.LogDebug($"Processed end event. Remaining stacks: {remainingStacksInfo}");

        return reportItem;
    }

    private ReportItem ProcessGap(CallGap gap)
    {
        var reportItem = new ReportItem
        {
            Id = string.Empty,
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

        return reportItem;
    }

    private void ApplyLimits()
    {
        if (_limitOptions.MaxItems.HasValue)
        {
            while (_reportItems.Count + Gaps.Count > _limitOptions.MaxItems.Value)
            {
                if (_reportItems.Count > 0)
                {
                    var oldestItem = _reportItems.Values.OrderBy(i => i.StartTime).FirstOrDefault();
                    if (oldestItem is not null && !string.IsNullOrEmpty(oldestItem.Id))
                    {
                        _reportItems.TryRemove(oldestItem.Id, out _);
                    }
                }
                else if (Gaps.Count > 0)
                {
                    Gaps.RemoveAt(0);
                }
            }
        }

        if (_limitOptions.MaxAge.HasValue)
        {
            var cutoffTime = DateTime.Now - _limitOptions.MaxAge.Value;
            var itemsToRemove = _reportItems.Values
                .Where(i => !string.IsNullOrEmpty(i.StartTime) && DateTime.TryParse(i.StartTime, out var startTime) && startTime < cutoffTime)
                .ToList();

            foreach (var item in itemsToRemove)
            {
                if (!string.IsNullOrEmpty(item.Id))
                {
                    _reportItems.TryRemove(item.Id, out _);
                }
            }

            Gaps.RemoveAll(g => !string.IsNullOrEmpty(g.StartTime) && DateTime.TryParse(g.StartTime, out var startTime) && startTime < cutoffTime);
        }
    }

    public void SetLimitOptions(OutputLimitOptions options)
    {
        _limitOptions = options;
        ApplyLimits();
    }

    public OutputLimitOptions GetLimitOptions()
    {
        return _limitOptions;
    }
}

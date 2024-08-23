namespace Orc.Monitoring.Reporters.ReportOutputs;

using System;
using System.Collections.Generic;
using System.Linq;
using Orc.Monitoring.MethodLifeCycleItems;
using Orc.Monitoring.Reporters;
using Microsoft.Extensions.Logging;

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

        _logger.LogInformation($"ReportOutputHelper initialized for reporter: {reporter.GetType().Name}");
    }

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

    public void AddReportItem(ReportItem item)
    {
        _reportItems.Add(item);
        _logger.LogDebug($"Added report item. Current count: {_reportItems.Count}");
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
        var id = methodCallInfo.Id ?? Guid.NewGuid().ToString();

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
        if (Reporter is null)
        {
            _logger.LogWarning("Reporter is null when processing end event.");
            return null;
        }

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

        if (_methodStack.TryGetValue(threadId, out var stack))
        {
            if (stack.Count > 0)
            {
                var poppedId = stack.Pop();
                if (poppedId != key)
                {
                    _logger.LogWarning($"Mismatch in method call IDs. Expected {key}, but popped {poppedId}.");
                }
            }
            else
            {
                _logger.LogWarning($"Attempted to process end event for thread {threadId} with an empty stack.");
            }

            if (stack.Count == 0)
            {
                _methodStack.Remove(threadId);
            }
        }
        else
        {
            _logger.LogWarning($"No stack found for thread {threadId} when processing end event.");
        }

        var remainingStacksInfo = string.Join(", ", _methodStack.Select(kvp => $"Thread {kvp.Key}: {kvp.Value.Count} items"));
        _logger.LogDebug($"Processed end event. Remaining stacks: {remainingStacksInfo}");

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

    private void ApplyLimits()
    {
        int removedItems = 0;
        int removedGaps = 0;

        if (_limitOptions.MaxItems.HasValue)
        {
            while (_reportItems.Count + Gaps.Count > _limitOptions.MaxItems.Value)
            {
                if (_reportItems.Count > 0)
                {
                    _reportItems.RemoveAt(0);
                    removedItems++;
                }
                else if (Gaps.Count > 0)
                {
                    Gaps.RemoveAt(0);
                    removedGaps++;
                }
            }
        }

        if (_limitOptions.MaxAge.HasValue)
        {
            var cutoffTime = DateTime.Now - _limitOptions.MaxAge.Value;
            removedItems += _reportItems.RemoveAll(i => !string.IsNullOrEmpty(i.StartTime) && DateTime.TryParse(i.StartTime, out var startTime) && startTime < cutoffTime);
            removedGaps += Gaps.RemoveAll(g => !string.IsNullOrEmpty(g.StartTime) && DateTime.TryParse(g.StartTime, out var startTime) && startTime < cutoffTime);
        }

        _logger.LogInformation($"Applied limits. Removed items: {removedItems}, Removed gaps: {removedGaps}");
        _logger.LogInformation($"Current ReportItems count: {_reportItems.Count}, Gaps count: {Gaps.Count}");
    }

    public void SetLimitOptions(OutputLimitOptions options)
    {
        _limitOptions = options;
        ApplyLimits();
        _logger.LogInformation($"Limit options set: MaxItems = {options.MaxItems}, MaxAge = {options.MaxAge}");
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
               $"LimitOptions: MaxItems={_limitOptions.MaxItems}, MaxAge={_limitOptions.MaxAge}";
    }
}

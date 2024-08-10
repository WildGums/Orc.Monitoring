namespace Orc.Monitoring.Reporters.ReportOutputs;

using System.Collections.Concurrent;
using System.Collections.Generic;
using MethodLifeCycleItems;
using Reporters;

public class ReportOutputHelper
{
    public IMethodCallReporter? Reporter { get; private set; }
    public ConcurrentDictionary<string, ReportItem> ReportItems { get; } = new();
    public List<ReportItem> Gaps { get; } = [];
    public ConcurrentDictionary<int, Stack<string>> MethodStack { get; } = new();
    public HashSet<string> ParameterNames { get; } = [];
    public string? LastEndTime { get; private set; }

    public void Initialize(IMethodCallReporter reporter)
    {
        Reporter = reporter;
        ReportItems.Clear();
        MethodStack.Clear();
        Gaps.Clear();
        ParameterNames.Clear();
        LastEndTime = null;
    }

    public void ProcessCallStackItem(ICallStackItem callStackItem)
    {
        switch (callStackItem)
        {
            case MethodCallStart start:
                ProcessStart(start);
                break;
            case MethodCallEnd end:
                ProcessEnd(end);
                break;
            case CallGap gap:
                ProcessGap(gap);
                break;
        }
    }

    private void ProcessStart(MethodCallStart start)
    {
        if (Reporter is null)
        {
            return;
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

        ReportItems.TryAdd(id, reportItem);

        if (!MethodStack.TryGetValue(threadId, out var stack))
        {
            stack = new Stack<string>();
            MethodStack.TryAdd(threadId, stack);
        }

        reportItem.Parent = methodCallInfo.Parent?.Id;
        reportItem.ParentThreadId = methodCallInfo.ParentThreadId.ToString();

        stack.Push(id);
    }

    private void ProcessEnd(MethodCallEnd end)
    {
        var methodCallInfo = end.MethodCallInfo;
        var key = methodCallInfo.Id ?? string.Empty;
        if (!ReportItems.TryGetValue(key, out var reportItem))
        {
            return;
        }

        var endTime = methodCallInfo.StartTime + methodCallInfo.Elapsed;
        LastEndTime = endTime.ToString("yyyy-MM-dd HH:mm:ss.fff");
        reportItem.EndTime = LastEndTime;
        reportItem.Duration = methodCallInfo.Elapsed.TotalMilliseconds.ToString("N1");
        reportItem.Parameters = methodCallInfo?.Parameters ?? [];
        reportItem.AttributeParameters = methodCallInfo?.AttributeParameters ?? [];

        ParameterNames.UnionWith(reportItem.Parameters.Keys);

        var threadId = methodCallInfo?.ThreadId ?? -1;
        if (MethodStack.TryGetValue(threadId, out var stack))
        {
            stack.Pop();
        }
    }

    private void ProcessGap(CallGap gap)
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
    }
}


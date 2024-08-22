namespace Orc.Monitoring.Reporters.ReportOutputs;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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

    public void ProcessEnd(MethodCallEnd end)
    {
        if (Reporter is null)
        {
            Console.WriteLine("Warning: Reporter is null when processing end event.");
            return;
        }

        var methodCallInfo = end.MethodCallInfo;
        var threadId = methodCallInfo.ThreadId;
        var key = methodCallInfo.Id ?? string.Empty;

        if (!ReportItems.TryGetValue(key, out var reportItem))
        {
            Console.WriteLine($"Warning: No report item found for method call ID {key}");
            return;
        }

        var endTime = methodCallInfo.StartTime + methodCallInfo.Elapsed;
        LastEndTime = endTime.ToString("yyyy-MM-dd HH:mm:ss.fff");
        reportItem.EndTime = LastEndTime;
        reportItem.Duration = methodCallInfo.Elapsed.TotalMilliseconds.ToString("N1");
        reportItem.Parameters = methodCallInfo.Parameters ?? new Dictionary<string, string>();
        reportItem.AttributeParameters = methodCallInfo.AttributeParameters ?? new HashSet<string>();

        ParameterNames.UnionWith(reportItem.Parameters.Keys);

        if (!MethodStack.TryGetValue(threadId, out var stack))
        {
            Console.WriteLine($"Warning: No stack found for thread {threadId} when processing end event.");
            return;
        }

        if (stack.Count == 0)
        {
            Console.WriteLine($"Warning: Attempted to process end event for thread {threadId} with an empty stack.");
            return;
        }

        var poppedId = stack.Pop();
        if (poppedId != key)
        {
            Console.WriteLine($"Warning: Mismatch in method call IDs. Expected {key}, but popped {poppedId}.");
            // Optionally, you might want to handle this mismatch, perhaps by searching for the correct ID in the stack
        }

        // If this was the last item on the stack for this thread, remove the stack
        if (stack.Count == 0)
        {
            MethodStack.TryRemove(threadId, out _);
        }

        // Log the current state after processing
        var remainingStacksInfo = string.Join(", ", MethodStack.Select(kvp => $"Thread {kvp.Key}: {kvp.Value.Count} items"));
        Console.WriteLine($"Processed end event. Remaining stacks: {remainingStacksInfo}");
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


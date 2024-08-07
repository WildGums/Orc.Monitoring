#pragma warning disable CTL0011
namespace Orc.Monitoring.Reporters;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using MethodLifeCycleItems;
using Monitoring;
using Filters;
using ReportOutputs;
using Microsoft.Extensions.Logging;

public sealed class WorkflowReporter : IMethodCallReporter
{
    private const int BatchSize = 100;

    private readonly ILogger<WorkflowReporter> _logger;

    private readonly StringBuilder _messageBuilder = new();
    private readonly Queue<IMethodLifeCycleItem> _itemBatch = new(BatchSize);
    private readonly List<IReportOutput> _outputs = new();
    private readonly List<IMethodFilter> _filters = new();
    private readonly Dictionary<int, int> _activeThreads = new();
    private readonly TaskCompletionSource _tcs = new();
#if DEBUG
    private readonly IReportOutput _debugOutput = new DebugCatelLogTraceReportOutput();
#endif

    private MethodInfo? _rootMethod;
    private MethodCallInfo? _rootMethodCallInfo;
    private CallProcessingContext? _callProcessingContext;
    private string? _rootWorkflowItemName;

    private bool _disposing;
#pragma warning disable IDISP006
    private AsyncCompositeDisposable? _disposables;
#pragma warning restore IDISP006

    public WorkflowReporter()
    {
        _logger = MonitoringController.CreateLogger<WorkflowReporter>();

        // Add default WorkflowItemFilter
        _filters.Add(new WorkflowItemFilter());
    }

    public void AddFilter(IMethodFilter filter)
    {
        _filters.Add(filter);
    }


    public string Name { get; } = "Workflow";
    public string FullName => $"{Name} - {GetRootWorkflowItemName() ?? string.Empty}";

    private string? GetRootWorkflowItemName()
    {
        var parameters = _rootMethodCallInfo?.Parameters;
        if (parameters is null)
        {
            return null;
        }

        return _rootWorkflowItemName ??= parameters?[MethodCallParameter.WorkflowItemName];
    }

    public MethodInfo? RootMethod
    {
        get => _rootMethod;
        set => _rootMethod = value;
    }

    public IAsyncDisposable StartReporting(IObservable<ICallStackItem> callStack)
    {
        if (_rootMethod is null)
        {
            throw new InvalidOperationException("Unable to start reporting when root method is not set");
        }

        _logger.LogInformation("WorkflowReporter started reporting");

        _disposables?.DisposeAsync().AsTask().Wait();
        _disposables = new AsyncCompositeDisposable();

        InitializeOutputs();

#if DEBUG
        if (_debugOutput is not null)
        {
            _disposables.Add(new AsyncDisposable(async () =>
            {
                callStack.Subscribe(
                    x => _debugOutput.WriteItem(x),
                    exception => _debugOutput.WriteError(exception)
                ).Dispose();
            }));
        }
#endif

        _disposables.Add(CreateReportingObservable(callStack));

        _disposables.Add(new AsyncDisposable(async () =>
        {

        }));

        return new AsyncDisposable(async () =>
        {
            _disposing = true;

            ProcessBatch();

            await _tcs.Task.ConfigureAwait(false);

            await _disposables.DisposeAsync();

            ProcessBatch();
        });
    }

    private void InitializeOutputs()
    {
        if (_outputs.Count == 0)
        {
            throw new InvalidOperationException("No outputs have been added to the reporter");
        }

        if (_disposables is null)
        {
            throw new InvalidOperationException("Reporter has not been initialized");
        }

        foreach (var reportOutput in _outputs)
        {
            _disposables.Add(reportOutput.Initialize(this));
        }

#if DEBUG
        _disposables.Add(_debugOutput.Initialize(this));
#endif
    }

    public IOutputContainer AddOutput<TOutput>(object? parameter = null) where TOutput : IReportOutput, new()
    {
        var output = new TOutput();
        output.SetParameters(parameter);
        _outputs.Add(output);

        return this;
    }

    private bool ShouldIncludeMethodCall(MethodCallInfo methodCallInfo)
    {
        return _filters.Any(filter => filter.ShouldInclude(methodCallInfo));
    }

    private IAsyncDisposable CreateReportingObservable(IObservable<ICallStackItem> callStack)
    {
        ArgumentNullException.ThrowIfNull(callStack);

        _callProcessingContext = new CallProcessingContext();

        var disposable = callStack.OfType<IMethodLifeCycleItem>()
            .Where(x => ShouldIncludeMethodCall(x.MethodCallInfo))
            .Subscribe(
                ProcessMethodLifeCycleItem,
                ex => _logger.LogError($"Error during summary reporting: {ex.Message}")
            );

        return new AsyncDisposable(async () => disposable.Dispose());
    }

    private void ProcessMethodLifeCycleItem(IMethodLifeCycleItem item)
    {
        _disposables?.Add(item.MethodCallInfo.Use());
        _itemBatch.Enqueue(item);

        if (_disposing || _itemBatch.Count >= BatchSize)
        {
            ProcessBatch();
        }
        else if (item is MethodCallEnd end && Equals(end.MethodCallInfo.MethodInfo, _rootMethod)) // Process the last batch
        {
            ProcessBatch();
        }
    }

    private void ProcessBatch()
    {
        while (_itemBatch.Count > 0)
        {
            var item = _itemBatch.Dequeue();
            switch (item)
            {
                case MethodCallStart start:
                    HandleMethodCallStart(start);
                    break;

                case MethodCallEnd end:
                    HandleMethodCallEnd(end);
                    break;
            }
        }

        if (_disposing && _itemBatch.Count == 0 && _activeThreads.Count == 0)
        {
            _tcs.TrySetResult();
        }
    }

    private void HandleMethodCallStart(MethodCallStart start)
    {
        if(_callProcessingContext is null)
        {
           throw new InvalidOperationException("Call processing context is not initialized");
        }

        var methodCallInfo = start.MethodCallInfo;
        if (_disposing && !_activeThreads.ContainsKey(methodCallInfo.ThreadId))
        {
            return;
        }

        IncrementActiveThreadCalls(methodCallInfo.ThreadId);

        if (Equals(methodCallInfo.MethodInfo, _rootMethod))
        {
            _rootMethodCallInfo = methodCallInfo;
            _callProcessingContext.GapStart = methodCallInfo.StartTime;
            PublishStartMethodCall(start);
        }
        else
        {
            HandleNonRootMethodCallStart(start);
        }
    }

    private void HandleNonRootMethodCallStart(MethodCallStart start)
    {
        if (_callProcessingContext is null)
        {
            throw new InvalidOperationException("Call processing context is not initialized");
        }

        var methodCallInfo = start.MethodCallInfo;
        var stack = _callProcessingContext.Stack;

        var gapEnd = methodCallInfo.StartTime;
        var gapStart = _callProcessingContext.GapStart ?? gapEnd;

        if (stack.Count == 0 && gapStart < gapEnd)
        {
            var gap = CreateCallGap(gapStart, gapEnd);

            PublishGap(gap);

            _callProcessingContext.GapsDuration += gap.Elapsed;
            _callProcessingContext.GapsCount++;
        }

        _callProcessingContext.GapStart = null;

        stack.TryAdd(methodCallInfo, default);
        PublishStartMethodCall(start);
    }

    private void HandleMethodCallEnd(MethodCallEnd end)
    {
        var methodCallInfo = end.MethodCallInfo;
        DecrementActiveThreadCalls(methodCallInfo.ThreadId);

        if (Equals(methodCallInfo.MethodInfo, _rootMethod))
        {
            HandleRootMethodCallEnd(end);
        }
        else
        {
            HandleNonRootMethodCallEnd(end);
        }
    }

    private void IncrementActiveThreadCalls(int threadId)
    {
        if (!_activeThreads.TryGetValue(threadId, out var counter))
        {
            _activeThreads.Add(threadId, 1);
        }
        else
        {
            _activeThreads[threadId] = counter + 1;
        }
    }

    private void DecrementActiveThreadCalls(int threadId)
    {
        if (_activeThreads.TryGetValue(threadId, out var counter))
        {
            if (counter == 1)
            {
                _activeThreads.Remove(threadId);
            }
            else
            {
                _activeThreads[threadId] = counter - 1;
            }
        }
    }

    private void HandleRootMethodCallEnd(MethodCallEnd end)
    {
        if (_callProcessingContext is null)
        {
            throw new InvalidOperationException("Call processing context is not initialized");
        }

        var rootMethodCallInfo = end.MethodCallInfo;

        var gapEnd = rootMethodCallInfo.StartTime + rootMethodCallInfo.Elapsed;
        var gapStart = _callProcessingContext.GapStart ?? gapEnd;

        if (_callProcessingContext.Stack.Count == 0 && gapStart < gapEnd)
        {
            var gap = CreateCallGap(gapStart, gapEnd);
            PublishGap(gap);

            _callProcessingContext.GapsDuration += gap.Elapsed;
            _callProcessingContext.GapsCount++;
        }

        PublishEndMethodCall(end);
        PublishSummary(rootMethodCallInfo);
    }

    private static CallGap CreateCallGap(DateTime gapStart, DateTime gapEnd) =>
        new(gapStart, gapEnd)
        {
            Parameters =
            {
                [MethodCallParameter.WorkflowItemName] = MethodCallParameter.Types.Gap,
                [MethodCallParameter.WorkflowItemType] = MethodCallParameter.Types.Gap
            }
        };

    private void HandleNonRootMethodCallEnd(MethodCallEnd end)
    {
        if (_callProcessingContext is null)
        {
            throw new InvalidOperationException("Call processing context is not initialized");
        }

        var endMethodCallInfo = end.MethodCallInfo;

        if (_callProcessingContext.Stack.Remove(endMethodCallInfo, out _) && _callProcessingContext.Stack.Count == 0)
        {
            _callProcessingContext.GapStart = endMethodCallInfo.StartTime + endMethodCallInfo.Elapsed;
        }

        var parameters = endMethodCallInfo.Parameters ?? [];
        var isUserInteraction = parameters.TryGetValue(MethodCallParameter.WorkflowItemType, out var itemType) &&
                                string.Equals(itemType, MethodCallParameter.Types.UserInteraction, StringComparison.Ordinal);

        if (isUserInteraction)
        {
            _callProcessingContext.UserInteractionDuration += endMethodCallInfo.Elapsed;
        }

        PublishEndMethodCall(end);
    }

    private void PublishToOutputs(Action<IReportOutput> action)
    {
        foreach (var output in _outputs)
        {
            if (MonitoringController.IsOutputTypeEnabled(output.GetType()))
            {
                action(output);
            }
        }
    }

    private void PublishGap(CallGap callGap)
    {
        PublishToOutputs(output => output.WriteItem(callGap));
    }

    private void PublishStartMethodCall(MethodCallStart methodCallStart)
    {
        var parameters = methodCallStart.MethodCallInfo.Parameters ?? [];
        if(!parameters.TryGetValue(MethodCallParameter.WorkflowItemName, out var workflowItemName))
        {
            return;
        }

        _messageBuilder.Clear()
            .Append('\'')
            .Append(workflowItemName)
            .Append("' started");

        var message = _messageBuilder.ToString();
        PublishToOutputs(output => output.WriteItem(methodCallStart, message));
    }

    private void PublishEndMethodCall(MethodCallEnd methodCallEnd)
    {
        var parameters = methodCallEnd.MethodCallInfo.Parameters ?? [];
        if (!parameters.TryGetValue(MethodCallParameter.WorkflowItemName, out var workflowItemName))
        {
            return;
        }

        _messageBuilder.Clear()
            .Append('\'')
            .Append(workflowItemName)
            .Append("' ended, took ")
            .Append(methodCallEnd.MethodCallInfo.Elapsed.TotalMilliseconds.ToString("N1"))
            .Append(" ms");

        var message = _messageBuilder.ToString();
        PublishToOutputs(output => output.WriteItem(methodCallEnd, message));
    }

    private void PublishSummary(MethodCallInfo rootMethodCallInfo)
    {
        if (_callProcessingContext is null)
        {
            throw new InvalidOperationException("Call processing context is not initialized");
        }

        var parameters = rootMethodCallInfo.Parameters ?? [];
        if (!parameters.TryGetValue(MethodCallParameter.WorkflowItemName, out var workflowItemName))
        {
            return;
        }

        _messageBuilder.Clear()
            .Append("Summary '")
            .Append(workflowItemName)
            .Append("' calculated total duration: ")
            .Append(_callProcessingContext.UserInteractionDuration.TotalMilliseconds.ToString("N1"))
            .Append(" ms");
        var message = _messageBuilder.ToString();
        PublishSummary(message);

        var gapsDuration = _callProcessingContext.GapsDuration;

        _messageBuilder.Clear()
            .Append("Summary '")
            .Append(workflowItemName)
            .Append("' calculated total gap duration: ")
            .Append(gapsDuration.TotalMilliseconds.ToString("N1"))
            .Append(" ms");
        message = _messageBuilder.ToString();
        PublishSummary(message);

        var userInteractionDuration = _callProcessingContext.UserInteractionDuration;

        _messageBuilder.Clear()
            .Append("Summary '")
            .Append(workflowItemName)
            .Append("' calculated total user interaction duration: ")
            .Append(userInteractionDuration.TotalMilliseconds.ToString("N1"))
            .Append(" ms");
        message = _messageBuilder.ToString();
        PublishSummary(message);

        var totalDuration = rootMethodCallInfo.Elapsed;

        _messageBuilder.Clear()
            .Append("Summary '")
            .Append(workflowItemName)
            .Append("' measured total duration: ")
            .Append(totalDuration.TotalMilliseconds.ToString("N1"))
            .Append(" ms");
        message = _messageBuilder.ToString();
        PublishSummary(message);

        var durationWithoutUserInteraction = totalDuration - userInteractionDuration;

        _messageBuilder.Clear()
            .Append("Summary '")
            .Append(workflowItemName)
            .Append("' measured duration without user interaction: ")
            .Append(durationWithoutUserInteraction.TotalMilliseconds.ToString("N1"))
            .Append(" ms");
        message = _messageBuilder.ToString();
        PublishSummary(message);

        _messageBuilder.Clear()
            .Append("Summary '")
            .Append(workflowItemName)
            .Append("' Gaps: ")
            .Append(_callProcessingContext.GapsCount);
        message = _messageBuilder.ToString();
        PublishSummary(message);
    }

    private void PublishSummary(string message)
    {
        PublishToOutputs(output => output.WriteSummary(message));
    }

    private class CallProcessingContext
    {
        public DateTime? GapStart { get; set; }
        public TimeSpan GapsDuration { get; set; } = TimeSpan.Zero;
        public TimeSpan UserInteractionDuration { get; set; } = TimeSpan.Zero;
        public int GapsCount { get; set; }
        public ConcurrentDictionary<MethodCallInfo, bool> Stack { get; } = new();
    }
}

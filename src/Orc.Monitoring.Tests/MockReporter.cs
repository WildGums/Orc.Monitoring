namespace Orc.Monitoring.Tests;

using System;
using System.Collections.Generic;
using System.Reflection;
using Orc.Monitoring.MethodLifeCycleItems;
using Orc.Monitoring.Reporters;
using Orc.Monitoring.Reporters.ReportOutputs;
using Orc.Monitoring.Filters;

public class MockReporter : IMethodCallReporter
{
    public string Name { get; set; } = "MockReporter";
    public string FullName { get; set; } = "MockReporter";
    public int StartReportingCallCount { get; private set; }
    public List<string> OperationSequence { get; } = new List<string>();
    public string? RootMethodName { get; private set; }
    public int CallCount { get; private set; }
    public Action<IObservable<ICallStackItem>>? OnStartReporting { get; set; }

    private MethodInfo? _rootMethod;
    private readonly List<IMethodFilter> _filters = new();

    public MethodInfo? RootMethod
    {
        get => _rootMethod;
        set
        {
            _rootMethod = value;
            if (value is not null)
            {
                Console.WriteLine($"SetRootMethod called for {value.Name}");
                OperationSequence.Add("SetRootMethod");
                RootMethodName = value.Name;
            }
        }
    }

    public IAsyncDisposable StartReporting(IObservable<ICallStackItem> callStack)
    {
        Console.WriteLine("StartReporting called");
        StartReportingCallCount++;
        CallCount++;
        OperationSequence.Add("StartReporting");
        OnStartReporting?.Invoke(callStack);
        return new AsyncDisposable(async () =>
        {
            Console.WriteLine("StartReporting disposing");
        });
    }

    public IOutputContainer AddOutput<TOutput>(object? parameter = null) where TOutput : IReportOutput, new()
    {
        return this;
    }

    public void AddFilter(IMethodFilter filter)
    {
        _filters.Add(filter);
        OperationSequence.Add($"AddFilter: {filter.GetType().Name}");
    }

    public void RemoveFilter(IMethodFilter filter)
    {
        _filters.Remove(filter);
        OperationSequence.Add($"RemoveFilter: {filter.GetType().Name}");
    }

    public IReadOnlyList<IMethodFilter> GetFilters()
    {
        OperationSequence.Add("GetFilters");
        return _filters.AsReadOnly();
    }

    public void Reset()
    {
        StartReportingCallCount = 0;
        CallCount = 0;
        OperationSequence.Clear();
        RootMethodName = null;
        OnStartReporting = null;
        _rootMethod = null;
        _filters.Clear();
    }
}

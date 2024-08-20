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
    private int _callCount;
    private string _id;

    public MockReporter()
    {
        
    }

    public string Id
    {
        get => _id;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Id cannot be null or whitespace.", nameof(value));
            }
            _id = value;
        }
    }

    public string Name { get; set; } = "MockReporter";
    public string FullName { get; set; } = "MockReporter";
    public int StartReportingCallCount { get; private set; }
    public List<string> OperationSequence { get; } = new List<string>();
    public string? RootMethodName { get; private set; }
    public int CallCount
    {
        get => _callCount;
        private set
        {
            _callCount = value;
            Console.WriteLine($"CallCount updated to: {_callCount} at {DateTime.Now:HH:mm:ss.fff}");
        }
    }
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
        Console.WriteLine($"StartReporting called for MockReporter (Id: {Id}) at {DateTime.Now:HH:mm:ss.fff}");
        CallCount++;
        StartReportingCallCount++;
        OperationSequence.Add("StartReporting");
        OnStartReporting?.Invoke(callStack);
        return new AsyncDisposable(async () =>
        {
            Console.WriteLine($"StartReporting disposing for MockReporter (Id: {Id}) at {DateTime.Now:HH:mm:ss.fff}");
            Console.WriteLine($"CallCount before dispose: {CallCount}");
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
        Console.WriteLine($"MockReporter Reset called at {DateTime.Now:HH:mm:ss.fff}");
        CallCount = 0;
        StartReportingCallCount = 0;
        OperationSequence.Clear();
        RootMethodName = null;
        OnStartReporting = null;
        _rootMethod = null;
    }
}

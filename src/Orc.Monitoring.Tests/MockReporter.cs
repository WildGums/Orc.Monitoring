namespace Orc.Monitoring.Tests;

using System;
using System.Collections.Generic;
using System.Reflection;
using MethodLifeCycleItems;
using Reporters;
using Reporters.ReportOutputs;
using Filters;
using Microsoft.Extensions.Logging;

public class MockReporter : IMethodCallReporter
{
    private readonly ILogger<MockReporter> _logger;

    private int _callCount;
    private string _id;
    private MonitoringConfiguration _monitoringConfiguration;

    public MockReporter(ILogger<MockReporter> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
        _logger.LogInformation($"MockReporter created at {DateTime.Now:HH:mm:ss.fff}");
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
            _logger.LogInformation($"CallCount updated to: {_callCount} at {DateTime.Now:HH:mm:ss.fff}");
        }
    }
    public Action<IObservable<ICallStackItem>>? OnStartReporting { get; set; }

    private MethodInfo? _rootMethod;
    private readonly List<Type> _filters = new();

    public MethodInfo? RootMethod
    {
        get => _rootMethod;
        set
        {
            _rootMethod = value;
            if (value is not null)
            {
                _logger.LogInformation($"SetRootMethod called for {value.Name}");
                OperationSequence.Add("SetRootMethod");
                RootMethodName = value.Name;
            }
        }
    }

    public IAsyncDisposable StartReporting(IObservable<ICallStackItem> callStack)
    {
        _logger.LogInformation($"StartReporting called for MockReporter (Id: {Id}) at {DateTime.Now:HH:mm:ss.fff}");
        CallCount++;
        StartReportingCallCount++;
        OperationSequence.Add("StartReporting");
        OnStartReporting?.Invoke(callStack);
        return new AsyncDisposable(async () =>
        {
            _logger.LogInformation($"StartReporting disposing for MockReporter (Id: {Id}) at {DateTime.Now:HH:mm:ss.fff}");
            _logger.LogInformation($"CallCount before dispose: {CallCount}");
        });
    }

    public void Initialize(MonitoringConfiguration monitoringConfiguration, MethodCallInfo rootMethod)
    {
        _monitoringConfiguration = monitoringConfiguration;
        RootMethod = rootMethod.MethodInfo;
    }

    public IOutputContainer AddFilter<T>() where T : IMethodFilter
    {
        _filters.Add(typeof(T));
        OperationSequence.Add($"AddFilter: {typeof(T).Name}");

        return this;
    }

    public void Initialize(MonitoringConfiguration monitoringConfiguration)
    {
        _monitoringConfiguration = monitoringConfiguration;
    }

    public IOutputContainer AddOutput<TOutput>(object? parameter = null) where TOutput : IReportOutput, new()
    {
        return this;
    }


    public void Reset()
    {
        _logger.LogInformation($"MockReporter Reset called at {DateTime.Now:HH:mm:ss.fff}");
        CallCount = 0;
        StartReportingCallCount = 0;
        OperationSequence.Clear();
        RootMethodName = null;
        OnStartReporting = null;
        _rootMethod = null;
    }
}

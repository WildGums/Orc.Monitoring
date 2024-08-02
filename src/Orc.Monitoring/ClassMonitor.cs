namespace Orc.Monitoring;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Orc.Monitoring.MethodLifeCycleItems;

internal class ClassMonitor : IClassMonitor
{
    private readonly HashSet<string> _methods;
    private readonly Type _classType;
    private readonly CallStack _callStack;
    private readonly bool _isExcluded;

    public ClassMonitor(Type classType, CallStack? callStack, HashSet<string> methods)
    {
        ArgumentNullException.ThrowIfNull(classType);
        ArgumentNullException.ThrowIfNull(callStack);
        ArgumentNullException.ThrowIfNull(methods);

        _classType = classType;
        _methods = methods;
        _callStack = callStack;
        _isExcluded = _methods.Count == 0;
        Console.WriteLine($"ClassMonitor created for {classType.Name}. IsExcluded: {_isExcluded}. Tracked methods: {string.Join(", ", _methods)}");
    }

    public AsyncMethodCallContext StartAsyncMethod(MethodConfiguration config, string callerMethod = "")
    {
        Console.WriteLine($"ClassMonitor.StartAsyncMethod called for {callerMethod}");
        return (AsyncMethodCallContext)StartMethodInternal(config, callerMethod, async: true);
    }

    public MethodCallContext StartMethod(MethodConfiguration config, [CallerMemberName] string callerMethod = "")
    {
        Console.WriteLine($"ClassMonitor.StartMethod called for {callerMethod}");
        return (MethodCallContext)StartMethodInternal(config, callerMethod, async: false);
    }

    private object StartMethodInternal(MethodConfiguration config, string callerMethod, bool async)
    {
        Console.WriteLine($"ClassMonitor.StartMethodInternal called for {callerMethod}. Async: {async}, Monitoring enabled: {MonitoringManager.IsEnabled}, Excluded: {_isExcluded}, Method tracked: {_methods.Contains(callerMethod)}");

        if (!MonitoringManager.IsEnabled || _isExcluded || !_methods.Contains(callerMethod))
        {
            Console.WriteLine("Returning Dummy context");
            return async
                ? AsyncMethodCallContext.Dummy
                : MethodCallContext.Dummy;
        }

        using var operation = MonitoringManager.BeginOperation();

        var methodCallInfo = _callStack.Push(this, _classType, new MethodCallContextConfig
        {
            ClassType = _classType,
            CallerMethodName = callerMethod,
            Reporters = config.Reporters,
            GenericArguments = config.GenericArguments,
            ParameterTypes = config.ParameterTypes
        });

        Console.WriteLine($"MethodCallInfo pushed. IsNull: {methodCallInfo.IsNull}, Version: {methodCallInfo.MonitoringVersion}");

        if (methodCallInfo.IsNull || !MonitoringManager.ShouldTrack(methodCallInfo.MonitoringVersion))
        {
            Console.WriteLine("Returning Dummy context due to null MethodCallInfo or outdated version");
            return async
                ? AsyncMethodCallContext.Dummy
                : MethodCallContext.Dummy;
        }

        var disposables = new List<IAsyncDisposable>();
        foreach (var reporter in config.Reporters)
        {
            Console.WriteLine($"Starting reporter: {reporter.GetType().Name}");
            reporter.RootMethod = methodCallInfo.MethodInfo;
            var reporterDisposable = reporter.StartReporting(_callStack);
            disposables.Add(reporterDisposable);
        }

        Console.WriteLine($"Returning {(async ? "async" : "sync")} context");
        return async
            ? new AsyncMethodCallContext(this, methodCallInfo, disposables)
            : new MethodCallContext(this, methodCallInfo, disposables);
    }

    public void LogStatus(IMethodLifeCycleItem status)
    {
        Console.WriteLine($"ClassMonitor.LogStatus called with {status.GetType().Name}");
        if (!MonitoringManager.IsEnabled || _isExcluded)
        {
            Console.WriteLine("Monitoring is disabled or class is excluded, not logging status");
            return;
        }

        ArgumentNullException.ThrowIfNull(status);

        if (status is MethodCallEnd endStatus && !endStatus.MethodCallInfo.IsNull)
        {
            Console.WriteLine($"Popping MethodCallInfo for {endStatus.MethodCallInfo}");
            _callStack.Pop(endStatus.MethodCallInfo);
        }

        Console.WriteLine($"Logging status to CallStack: {status}");
        _callStack.LogStatus(status);
    }
}

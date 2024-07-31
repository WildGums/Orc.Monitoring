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
    }

    public AsyncMethodCallContext StartAsyncMethod(MethodConfiguration config, string callerMethod = "") =>
        (AsyncMethodCallContext)StartMethodInternal(config, callerMethod, async: true);

    public MethodCallContext StartMethod(MethodConfiguration config, [CallerMemberName] string callerMethod = "") =>
        (MethodCallContext)StartMethodInternal(config, callerMethod, async: false);

    private object StartMethodInternal(MethodConfiguration config, string callerMethod, bool async)
    {
        if (_isExcluded || !_methods.Contains(callerMethod))
        {
            return async 
                ? AsyncMethodCallContext.Dummy 
                : MethodCallContext.Dummy;
        }

        var methodCallInfo = _callStack.Push(this, _classType, new MethodCallContextConfig
        {
            ClassType = _classType,
            CallerMethodName = callerMethod,
            Reporters = config.Reporters,
            GenericArguments = config.GenericArguments,
            ParameterTypes = config.ParameterTypes
        });

        var disposables = new List<IAsyncDisposable>();
        foreach (var reporter in config.Reporters)
        {
            reporter.RootMethod = methodCallInfo.MethodInfo;
            disposables.Add(reporter.StartReporting(_callStack));
        }

        return async
            ? methodCallInfo.StartAsync(disposables)
            : methodCallInfo.Start(disposables);
    }

    public void LogStatus(IMethodLifeCycleItem status)
    {
        if (_isExcluded)
        {
            return;
        }

        ArgumentNullException.ThrowIfNull(status);

        if (status is MethodCallEnd)
        {
            _callStack.Pop(status.MethodCallInfo);
        }

        _callStack.LogStatus(status);
    }
}

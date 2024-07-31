#pragma warning disable CTL0011
#pragma warning disable CTL0011
namespace Orc.Monitoring;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime;
using System.Text;
using Catel.Logging;
using MethodLifeCycleItems;

public class CallStack : IObservable<ICallStackItem>
{
    private static readonly ILog Log = LogManager.GetCurrentClassLogger();

    private readonly MethodCallInfoPool _methodCallInfoPool = new();
    private readonly ConcurrentDictionary<int, Stack<MethodCallInfo>> _threadCallStacks = new();
    private readonly ConcurrentStack<MethodCallInfo> _globalCallStack = new();
    private readonly object _idLock = new();
    private readonly List<IObserver<ICallStackItem>> _observers = new();

    private int _idCounter = 0;

    public CallStack()
    {
        
    }

    public MethodCallInfo Push(IClassMonitor classMonitor, Type callerType, MethodCallContextConfig config)
    {
        var threadId = Environment.CurrentManagedThreadId;
        var threadStack = _threadCallStacks.GetOrAdd(threadId, _ => new Stack<MethodCallInfo>());

        var callerMethodName = config.CallerMethodName;
        var genericArguments = config.GenericArguments;

        var methodInfo = FindMatchingMethod(config);

        if (methodInfo is null)
        {
            var classTypeName = config.ClassType?.Name ?? string.Empty;
            throw new InvalidOperationException($"Method {callerMethodName} not found in {classTypeName} with the specified parameter types");
        }

        var attributeParameters = methodInfo.GetCustomAttributes<MethodCallParameterAttribute>()
            .ToDictionary(attr => attr.Name, attr => attr.Value);

        lock (threadStack)
        {
            var level = threadStack.Count + 1;
            var id = GenerateId();

            MethodCallInfo? parentMethodCallInfo = null;
            if (threadStack.Count > 0)
            {
                parentMethodCallInfo = threadStack.Peek();
            }
            else if (_globalCallStack.TryPeek(out var globalParent))
            {
                parentMethodCallInfo = globalParent;
            }

            var methodCallInfo = _methodCallInfoPool.Rent(classMonitor, callerType, methodInfo, genericArguments, level, id, parentMethodCallInfo, attributeParameters);

            threadStack.Push(methodCallInfo);
            _globalCallStack.Push(methodCallInfo);

            return methodCallInfo;
        }
    }

    public void Pop(MethodCallInfo methodCallInfo)
    {
        ArgumentNullException.ThrowIfNull(methodCallInfo);

        var threadId = methodCallInfo.ThreadId;

        if (_threadCallStacks.TryGetValue(threadId, out var threadStack))
        {
            lock (threadStack)
            {
                if (threadStack.Count > 0)
                {
                    var poppedContext = threadStack.Pop();

                    if (poppedContext != methodCallInfo)
                    {
                        Log.Warning($"Thread CallStack mismatch: popped context is not the same as the method call info.");
                    }
                }
                else
                {
                    Log.Warning($"Thread CallStack mismatch: no context found for thread {threadId}.");
                }
            }
        }
        else
        {
            Log.Warning($"Thread CallStack mismatch: no stack found for thread {threadId}.");
        }

        // Always try to pop from the global stack
        if (_globalCallStack.TryPop(out var globalPoppedContext))
        {
            if (globalPoppedContext != methodCallInfo)
            {
                Log.Warning("Global CallStack mismatch: popped context is not the same as the method call info.");
            }
        }
        else
        {
            Log.Warning("Global CallStack mismatch: failed to pop method call info.");
        }
    }

    public void LogStatus(IMethodLifeCycleItem status)
    {
        ArgumentNullException.ThrowIfNull(status);

        NotifyObservers(status);

        if (status is MethodCallEnd && IsEmpty())
        {
            NotifyObservers(CallStackItem.Empty);
        }
    }

    private MethodInfo? FindMatchingMethod(MethodCallContextConfig config)
    {
        var bindingFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        var classType = config.ClassType;
        var methodName = config.CallerMethodName;

        if (classType is null)
        {
            return null;
        }


        // First, try to find the method in the current class
        var currentClassMethods = classType.GetMethods(bindingFlags)
            .Where(m => m.Name == methodName)
            .ToList();

        var matchedMethod = FindBestMatch(currentClassMethods, config);
        if (matchedMethod is not null)
        {
            return matchedMethod;
        }

        // If not found in the current class, search through the hierarchy
        var allMethods = new List<MethodInfo>();
        var currentType = classType.BaseType;

        while (currentType is not null)
        {
            allMethods.AddRange(currentType.GetMethods(bindingFlags).Where(m => m.Name == methodName));
            currentType = currentType.BaseType;
        }

        matchedMethod = FindBestMatch(allMethods, config);
        if (matchedMethod is not null)
        {
            return matchedMethod;
        }

        // If we still can't determine, throw an exception
        var classTypeName = config.ClassType?.Name ?? string.Empty;
        throw new InvalidOperationException($"No matching method named {methodName} found in {classTypeName} or its base classes that matches the provided configuration.");
    }

    private MethodInfo? FindBestMatch(List<MethodInfo> methods, MethodCallContextConfig config)
    {
        if (!methods.Any())
        {
            return null;
        }

        if (methods.Count == 1)
        {
            return methods[0];
        }

        // If we have multiple methods, try to match based on parameter types
        if (config.ParameterTypes.Any())
        {
            var matchedMethod = methods.FirstOrDefault(m => ParametersMatch(m.GetParameters(), config.ParameterTypes));
            if (matchedMethod is not null)
            {
                return matchedMethod;
            }
        }

        // If we still can't determine, and it's a generic method, try to match based on generic arguments
        if (config.GenericArguments.Any())
        {
            var matchedMethod = methods.FirstOrDefault(m => GenericArgumentsMatch(m, config.GenericArguments));
            if (matchedMethod is not null)
            {
                return matchedMethod;
            }
        }

        var classTypeName = config.ClassType?.Name ?? string.Empty;

        throw new AmbiguousImplementationException(
            $"Multiple methods found in {classTypeName} with the name {methods[0].Name} that match the provided configuration.");
    }

    private bool ParametersMatch(ParameterInfo[] methodParams, IReadOnlyCollection<Type> configParams)
    {
        if (methodParams.Length != configParams.Count)
        {
            return false;
        }

        for (var i = 0; i < methodParams.Length; i++)
        {
            if (!methodParams[i].ParameterType.IsAssignableFrom(configParams.ElementAt(i)))
            {
                return false;
            }
        }

        return true;
    }

    private bool GenericArgumentsMatch(MethodInfo method, IReadOnlyCollection<Type> configGenericArgs)
    {
        if (!method.IsGenericMethod)
        {
            return false;
        }

        var genericArgs = method.GetGenericArguments();
        return genericArgs.Length == configGenericArgs.Count;
    }

    private bool IsEmpty() => _threadCallStacks.Values.All(t => t.Count == 0);

    private string GenerateId()
    {
        lock (_idLock)
        {
            var number = _idCounter++;
            var sb = new StringBuilder();
            while (number >= 0)
            {
                sb.Insert(0, (char)('A' + number % 26));
                number = number / 26 - 1;
            }
            return sb.ToString();
        }
    }

    public IDisposable Subscribe(IObserver<ICallStackItem> observer)
    {
        if (!_observers.Contains(observer))
        {
            _observers.Add(observer);
        }

        return new Unsubscriber<ICallStackItem>(_observers, observer);
    }

    private void NotifyObservers(ICallStackItem value)
    {
        foreach (var observer in _observers.ToArray())
        {
            observer.OnNext(value);
        }
    }

    private sealed class Unsubscriber<T> : IDisposable
    {
        private readonly List<IObserver<T>> _observers;
        private readonly IObserver<T> _observer;

        public Unsubscriber(List<IObserver<T>> observers, IObserver<T> observer)
        {
            _observers = observers;
            _observer = observer;
        }

        public void Dispose()
        {
            if (_observer is not null && _observers.Contains(_observer))
            {
                _observers.Remove(_observer);
            }
        }
    }
}

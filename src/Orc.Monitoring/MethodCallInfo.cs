namespace Orc.Monitoring;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reflection;
using Reporters;

public class MethodCallInfo
{
    private readonly MethodCallInfoPool _pool;

    private IClassMonitor? _classMonitor;
    private int _usageCounter;
    private bool _readyToReturn;

    public MethodCallInfo(MethodCallInfoPool pool, IClassMonitor classMonitor, Type classType, MethodInfo methodInfo,
        IReadOnlyCollection<Type> genericArguments, int level, string id, MethodCallInfo? parent,
        Dictionary<string, string> attributeParameters)
    {
        ArgumentNullException.ThrowIfNull(pool);
        ArgumentNullException.ThrowIfNull(classType);

        _pool = pool;

        Reset(classMonitor, classType, methodInfo, genericArguments, level, id, parent, attributeParameters);
    }

    public Dictionary<string, string>? Parameters { get; set; }

    public MethodInfo? MethodInfo { get; set; }

    public Type? ClassType { get; set; }

    public string? MethodName { get; set; }

    public int ThreadId { get; set; }

    public DateTime StartTime { get; set; }

    public int Level { get; set; }

    public string? Id { get; set; }

    public TimeSpan Elapsed { get; set; }

    public MethodCallInfo? Parent { get; set; }

    public int ParentThreadId { get; set; }

    public HashSet<string>? AttributeParameters { get; private set; }


    public void Reset(IClassMonitor classMonitor, Type classType, MethodInfo methodInfo,
        IReadOnlyCollection<Type> genericArguments, int level, string id, MethodCallInfo? parent,
        Dictionary<string, string> attributeParameters)
    {
        // Reset all properties
        _readyToReturn = false;
        _classMonitor = classMonitor;
        ClassType = classType;
        MethodName = GetMethodName(methodInfo, genericArguments);
        MethodInfo = methodInfo;
        StartTime = DateTime.Now;
        Level = level;
        Id = id;
        ThreadId = Environment.CurrentManagedThreadId;
        Elapsed = TimeSpan.Zero;
        Parent = parent;
        ParentThreadId = parent?.ThreadId ?? -1;

        AttributeParameters = new HashSet<string>(attributeParameters.Keys);
        Parameters = new Dictionary<string, string>(attributeParameters);
    }

    public void Clear()
    {
        // Clear all properties to default values
        _readyToReturn = false;
        _classMonitor = null;
        ClassType = null;
        MethodName = null;
        MethodInfo = null;
        StartTime = default;
        Level = 0;
        Id = null;
        ThreadId = 0;
        Parameters?.Clear();
        Parameters = null;
        AttributeParameters?.Clear();
        AttributeParameters = null;
        Elapsed = TimeSpan.Zero;
        Parent = null;
        ParentThreadId = -1;
    }

    public void TryReturnToPool()
    {
        _readyToReturn = true;
        if (_usageCounter == 0)
        {
            _pool.Return(this);
        }
    }

    public IAsyncDisposable Use()
    {
        _usageCounter++;

        return new AsyncDisposable(async () =>
        {
            _usageCounter--;
            if (_usageCounter == 0 && _readyToReturn)
            {
                _pool.Return(this);
            }
        });
    }

    public MethodCallContext Start(List<IAsyncDisposable> disposables) => new(_classMonitor, this, disposables);
    public AsyncMethodCallContext StartAsync(List<IAsyncDisposable> disposables) => new(_classMonitor, this, disposables);

    public override string ToString()
    {
        var classTypeName = ClassType?.Name ?? string.Empty;
        return $"{classTypeName}.{MethodName}";
    }

    private static string GetMethodName(MethodInfo methodInfo, IReadOnlyCollection<Type> genericArguments)
    {
        var methodName = methodInfo.Name;

        if (genericArguments.Count != 0)
        {
            var genericArgumentsNames = genericArguments.Select(a => a.Name);
            methodName = $"{methodName}<{string.Join(", ", genericArgumentsNames)}>";
        }

        var parameters = methodInfo.GetParameters();
        if (parameters.Length != 0)
        {
            var parametersNames = parameters.Select(p => p.ParameterType.Name);
            methodName = $"{methodName}({string.Join(", ", parametersNames)})";
        }
        else
        {
            methodName = $"{methodName}()";
        }

        return methodName;
    }
}

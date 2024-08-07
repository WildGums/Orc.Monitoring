#pragma warning disable CS0414 // Field is assigned but its value is never used
#pragma warning disable CS0169 // Field is never used
namespace Orc.Monitoring;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Orc.Monitoring.Reporters;


public class MethodCallInfo
{
    private readonly MethodCallInfoPool? _pool;
    private IClassMonitor? _classMonitor;
    private int _usageCounter;
    private bool _readyToReturn;

    public bool IsNull { get; private set; }
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
    public MonitoringVersion Version { get; private set; }

    private MethodCallInfo(MethodCallInfoPool? pool)
    {
        _pool = pool;
    }

    public static MethodCallInfo CreateNull()
    {
        return new MethodCallInfo(null)
        {
            IsNull = true
        };
    }

    public static MethodCallInfo Create(MethodCallInfoPool pool, IClassMonitor classMonitor, Type classType, MethodInfo methodInfo,
        IReadOnlyCollection<Type> genericArguments, int level, string id, MethodCallInfo? parent,
        Dictionary<string, string> attributeParameters)
    {
        var info = new MethodCallInfo(pool);
        info.Reset(classMonitor, classType, methodInfo, genericArguments, level, id, parent, attributeParameters);
        return info;
    }

    public void Reset(IClassMonitor classMonitor, Type classType, MethodInfo methodInfo,
        IReadOnlyCollection<Type> genericArguments, int level, string id, MethodCallInfo? parent,
        Dictionary<string, string> attributeParameters)
    {
        if (IsNull) return;

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
        Version = MonitoringController.GetCurrentVersion();
    }

    public void Clear()
    {
        if (IsNull) return;

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
        Version = default;
    }

    public void TryReturnToPool()
    {
        if (IsNull) return;

        _readyToReturn = true;
        if (_usageCounter == 0)
        {
            _pool?.Return(this);
        }
    }

    public IAsyncDisposable Use()
    {
        if (IsNull)
        {
            return new AsyncDisposable(async () => { });
        }

        _usageCounter++;

        return new AsyncDisposable(async () =>
        {
            _usageCounter--;
            if (_usageCounter == 0 && _readyToReturn)
            {
                _pool?.Return(this);
            }
        });
    }

    public MethodCallContext Start(List<IAsyncDisposable> disposables)
    {
        return IsNull ? MethodCallContext.Dummy : new MethodCallContext(_classMonitor, this, disposables);
    }

    public AsyncMethodCallContext StartAsync(List<IAsyncDisposable> disposables)
    {
        return IsNull ? AsyncMethodCallContext.Dummy : new AsyncMethodCallContext(_classMonitor, this, disposables);
    }

    public override string ToString()
    {
        if (IsNull) return "Null MethodCallInfo";
        var classTypeName = ClassType?.Name ?? string.Empty;
        return $"{classTypeName}.{MethodName} (Version: {Version})";
    }

    private static string GetMethodName(MethodInfo methodInfo, IReadOnlyCollection<Type> genericArguments)
    {
        var methodName = methodInfo.Name;

        if (genericArguments.Count != 0)
        {
            var genericArgumentsNames = string.Join(", ", genericArguments.Select(a => a.Name));
            methodName = $"{methodName}<{genericArgumentsNames}>";
        }

        var parameters = methodInfo.GetParameters();
        if (parameters.Length != 0)
        {
            var parametersNames = string.Join(", ", parameters.Select(p => p.ParameterType.Name));
            methodName = $"{methodName}({parametersNames})";
        }
        else
        {
            methodName = $"{methodName}()";
        }

        return methodName;
    }
}

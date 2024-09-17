#pragma warning disable CS0414 // Field is assigned but its value is never used
#pragma warning disable CS0169 // Field is never used
namespace Orc.Monitoring;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Reporters;

public class MethodCallInfo
{
    private MethodCallInfo? _parent;
    private readonly HashSet<IMethodCallReporter> _associatedReporters = [];
    private Dictionary<string, string>? _parameters;

    public bool IsNull { get; init; }

    public IReadOnlyDictionary<string, string>? Parameters
    {
        get => _parameters;
        set => _parameters = value is null ? null : new Dictionary<string, string>(value);
    }

    public MethodInfo? MethodInfo { get; set; }
    public Type? ClassType { get; set; }
    public string? MethodName { get; set; }
    public int ThreadId { get; set; }
    public DateTime StartTime { get; set; }
    public int Level { get; set; }
    public string? Id { get; set; }
    public TimeSpan Elapsed { get; set; }
    public MethodCallInfo? Parent
    {
        get => _parent;
        set
        {
            _parent = value;
            ParentThreadId = value?.ThreadId ?? -1;
        }
    }

    public int ParentThreadId { get; set; } = -1;
    public HashSet<string>? AttributeParameters { get; private set; }
    public MonitoringVersion Version { get; private set; }

    // Properties for static, generic, and extension methods
    public bool IsStatic { get; set; }
    public bool IsGenericMethod { get; set; }
    public Type[]? GenericArguments { get; private set; }
    public bool IsExtensionMethod { get; set; }
    public Type? ExtendedType { get; set; }

    // New properties for external method calls
    public bool IsExternalCall { get; set; }
    public string? ExternalTypeName { get; set; }

    public IReadOnlyCollection<IMethodCallReporter> AssociatedReporters => _associatedReporters;

    public bool ReadyToReturn { get; set; }
    public int UsageCounter;

    public void Reset(IMonitoringController monitoringController, IClassMonitor? classMonitor, Type classType, MethodInfo methodInfo,
        IReadOnlyCollection<Type> genericArguments, string id, Dictionary<string, string> attributeParameters,
        bool isExternalCall = false, string? externalTypeName = null)
    {
        if (IsNull)
        {
            return;
        }

        ReadyToReturn = false;
        ClassType = classType;
        MethodName = GetMethodName(methodInfo, genericArguments);
        MethodInfo = methodInfo;
        StartTime = DateTime.Now;
        Id = id;
        ThreadId = Environment.CurrentManagedThreadId;
        Elapsed = TimeSpan.Zero;

        AttributeParameters = [.. attributeParameters.Keys];
        Parameters = new Dictionary<string, string>(attributeParameters);
        Version = monitoringController.GetCurrentVersion();

        // Set properties for static, generic, and extension methods
        IsStatic = methodInfo.IsStatic;
        IsGenericMethod = methodInfo.IsGenericMethod;
        GenericArguments = IsGenericMethod ? methodInfo.GetGenericArguments() : null;
        IsExtensionMethod = methodInfo.IsDefined(typeof(ExtensionAttribute), false);
        ExtendedType = IsExtensionMethod ? methodInfo.GetParameters()[0].ParameterType : null;

        // Set properties for external method calls
        IsExternalCall = isExternalCall;
        ExternalTypeName = externalTypeName;
    }

    public void Clear()
    {
        if (IsNull)
        {
            return;
        }

        ReadyToReturn = false;
        ClassType = null;
        MethodName = null;
        MethodInfo = null;
        StartTime = default;
        Level = 0;
        Id = null;
        ThreadId = 0;
        _parameters?.Clear();
        Parameters = null;
        AttributeParameters?.Clear();
        AttributeParameters = null;
        Elapsed = TimeSpan.Zero;
        Parent = null;
        ParentThreadId = -1;
        Version = default;

        // Clear properties for static, generic, and extension methods
        IsStatic = false;
        IsGenericMethod = false;
        GenericArguments = null;
        IsExtensionMethod = false;
        ExtendedType = null;

        // Clear properties for external method calls
        IsExternalCall = false;
        ExternalTypeName = null;
    }

    public void SetGenericArguments(Type[] genericArguments)
    {
        IsGenericMethod = true;
        GenericArguments = genericArguments;
    }

    public override string ToString()
    {
        if (IsNull) return "Null MethodCallInfo";
        var classTypeName = IsExternalCall ? ExternalTypeName : ClassType?.Name ?? string.Empty;
        var methodType = IsStatic ? "Static" : IsExtensionMethod ? "Extension" : "Instance";
        var genericInfo = IsGenericMethod ? $"<{string.Join(", ", GenericArguments?.Select(t => t.Name) ?? Array.Empty<string>())}>" : string.Empty;
        var externalInfo = IsExternalCall ? " (External)" : string.Empty;
        return $"{methodType} {classTypeName}.{MethodName}{genericInfo}{externalInfo} (Id: {Id}, ThreadId: {ThreadId}, ParentId: {Parent?.Id ?? "None"}, Level: {Level}, Version: {Version})";
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

    public override bool Equals(object? obj)
    {
        if (obj is not MethodCallInfo other)
        {
            return false;
        }

        if (IsNull && other.IsNull)
        {
            return true;
        }

        if (IsNull || other.IsNull)
        {
            return false;
        }

        // Compare relevant properties
        return Id == other.Id &&
               ThreadId == other.ThreadId &&
               ParentThreadId == other.ParentThreadId &&
               Level == other.Level &&
               MethodName == other.MethodName &&
               IsStatic == other.IsStatic &&
               IsGenericMethod == other.IsGenericMethod &&
               IsExtensionMethod == other.IsExtensionMethod &&
               IsExternalCall == other.IsExternalCall &&
               ExternalTypeName == other.ExternalTypeName;
    }

    public override int GetHashCode()
    {
        if (IsNull)
        {
            return 0;
        }

        unchecked // Overflow is fine, just wrap
        {
            int hash = 17;
            hash = hash * 23 + (Id?.GetHashCode() ?? 0);
            hash = hash * 23 + ThreadId.GetHashCode();
            hash = hash * 23 + ParentThreadId.GetHashCode();
            hash = hash * 23 + Level.GetHashCode();
            hash = hash * 23 + (MethodName?.GetHashCode() ?? 0);
            hash = hash * 23 + IsStatic.GetHashCode();
            hash = hash * 23 + IsGenericMethod.GetHashCode();
            hash = hash * 23 + IsExtensionMethod.GetHashCode();
            hash = hash * 23 + IsExternalCall.GetHashCode();
            hash = hash * 23 + (ExternalTypeName?.GetHashCode() ?? 0);
            return hash;
        }
    }

    public static bool operator ==(MethodCallInfo? left, MethodCallInfo? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null)
        {
            return right is null || right.IsNull;
        }

        return right is null
            ? left.IsNull
            : left.Equals(right);
    }

    public static bool operator !=(MethodCallInfo? left, MethodCallInfo? right)
    {
        return !(left == right);
    }

    public void AddAssociatedReporter(IMethodCallReporter reporter)
    {
        _associatedReporters.Add(reporter);
    }

    public void AddParameter(string attrName, string attrValue)
    {
        if (_parameters is null)
        {
            _parameters = new Dictionary<string, string>();
        }

        _parameters[attrName] = attrValue;
    }
}

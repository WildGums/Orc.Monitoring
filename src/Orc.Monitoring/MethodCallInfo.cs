﻿#pragma warning disable CS0414 // Field is assigned but its value is never used
#pragma warning disable CS0169 // Field is never used
namespace Orc.Monitoring;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.VisualBasic;
using Orc.Monitoring.Reporters;


public class MethodCallInfo
{
    private IClassMonitor? _classMonitor;
    private MethodCallInfo? _parent;
    private readonly HashSet<IMethodCallReporter> _associatedReporters = new HashSet<IMethodCallReporter>();

    public bool IsNull { get; init; }
    public Dictionary<string, string>? Parameters { get; set; }
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

    // New properties for static, generic, and extension methods
    public bool IsStatic { get; set; }
    public bool IsGenericMethod { get; set; }
    public Type[]? GenericArguments { get; private set; }
    public bool IsExtensionMethod { get; set; }
    public Type? ExtendedType { get; set; }

    public IReadOnlyCollection<IMethodCallReporter> AssociatedReporters => _associatedReporters;

    public bool ReadyToReturn { get; set; }
    public int UsageCounter { get; set; }

    public void Reset(IMonitoringController monitoringController, IClassMonitor? classMonitor, Type classType, MethodInfo methodInfo,
        IReadOnlyCollection<Type> genericArguments, string id, Dictionary<string, string> attributeParameters)
    {
        if (IsNull)
        {
            return;
        }

        ReadyToReturn = false;
        _classMonitor = classMonitor;
        ClassType = classType;
        MethodName = GetMethodName(methodInfo, genericArguments);
        MethodInfo = methodInfo;
        StartTime = DateTime.Now;
        Id = id;
        ThreadId = Environment.CurrentManagedThreadId;
        Elapsed = TimeSpan.Zero;

        AttributeParameters = new HashSet<string>(attributeParameters.Keys);
        Parameters = new Dictionary<string, string>(attributeParameters);
        Version = monitoringController.GetCurrentVersion();

        // Set properties for static, generic, and extension methods
        IsStatic = methodInfo.IsStatic;
        IsGenericMethod = methodInfo.IsGenericMethod;
        GenericArguments = IsGenericMethod ? methodInfo.GetGenericArguments() : null;
        IsExtensionMethod = methodInfo.IsDefined(typeof(ExtensionAttribute), false);
        ExtendedType = IsExtensionMethod ? methodInfo.GetParameters()[0].ParameterType : null;
    }

    public void Clear()
    {
        if (IsNull)
        {
            return;
        }

        ReadyToReturn = false;
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

        // Clear properties for static, generic, and extension methods
        IsStatic = false;
        IsGenericMethod = false;
        GenericArguments = null;
        IsExtensionMethod = false;
        ExtendedType = null;
    }

    public void SetGenericArguments(Type[] genericArguments)
    {
        IsGenericMethod = true;
        GenericArguments = genericArguments;
    }

    public override string ToString()
    {
        if (IsNull) return "Null MethodCallInfo";
        var classTypeName = ClassType?.Name ?? string.Empty;
        var methodType = IsStatic ? "Static" : IsExtensionMethod ? "Extension" : "Instance";
        var genericInfo = IsGenericMethod ? $"<{string.Join(", ", GenericArguments?.Select(t => t.Name) ?? Array.Empty<string>())}>" : string.Empty;
        return $"{methodType} {classTypeName}.{MethodName}{genericInfo} (Id: {Id}, ThreadId: {ThreadId}, ParentId: {Parent?.Id ?? "None"}, Level: {Level}, Version: {Version})";
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

    public bool IsRootForReporter(IMethodCallReporter reporter)
    {
        return AssociatedReporters.Contains(reporter);
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
               IsExtensionMethod == other.IsExtensionMethod;
    }

    public override int GetHashCode()
    {
        return IsNull switch
        {
            true => 0,
            _ => HashCode.Combine(Id, ThreadId, ParentThreadId, Level, MethodName, IsStatic, IsGenericMethod, IsExtensionMethod)
        };
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
}

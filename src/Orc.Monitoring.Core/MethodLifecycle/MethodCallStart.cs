namespace Orc.Monitoring.Core.MethodLifecycle;

using System;
using Models;

/// <summary>
/// Represents the start of a method call in the performance monitoring system.
/// </summary>
public class MethodCallStart : MethodLifeCycleItemBase
{
    public MethodCallStart(MethodCallInfo methodCallInfo)
    : base(methodCallInfo)
    {
        ArgumentNullException.ThrowIfNull(methodCallInfo);

        TimeStamp = methodCallInfo.StartTime;
    }

    public override string ToString() => $"MethodCallStart at {TimeStamp}: {MethodCallInfo}";
}


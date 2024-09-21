namespace Orc.Monitoring.Core.MethodLifecycle;

using System;
using Models;

/// <summary>
/// Represents the end of a method call in the performance monitoring system.
/// </summary>
public class MethodCallEnd : MethodLifeCycleItemBase
{
    public MethodCallEnd(MethodCallInfo methodCallInfo)
    : base(methodCallInfo)
    {
        ArgumentNullException.ThrowIfNull(methodCallInfo);

        TimeStamp = methodCallInfo.StartTime + methodCallInfo.Elapsed;
    }

    public override string ToString() => $"MethodCallEnd at {TimeStamp}: {MethodCallInfo}";
}

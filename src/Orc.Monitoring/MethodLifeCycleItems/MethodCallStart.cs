namespace Orc.Monitoring.MethodLifeCycleItems;

using System;
using System.Threading;
using Orc.Monitoring;

/// <summary>
/// Represents the start of a method call in the performance monitoring system.
/// </summary>
public class MethodCallStart : IMethodLifeCycleItem
{
    public MethodCallStart(MethodCallInfo methodCallInfo)
    {
        ArgumentNullException.ThrowIfNull(methodCallInfo);

        TimeStamp = methodCallInfo.StartTime;
        MethodCallInfo = methodCallInfo;
        ThreadId = Thread.CurrentThread.ManagedThreadId;
    }

    /// <summary>
    /// Gets the timestamp of the start of the method call.
    /// </summary>
    public DateTime TimeStamp { get; }

    public MethodCallInfo MethodCallInfo { get; }

    /// <summary>
    /// Gets the ID of the thread that started the method call.
    /// </summary>
    public int ThreadId { get; }

    public override string ToString() => $"MethodCallStart: {MethodCallInfo}";
}


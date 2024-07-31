namespace Orc.Monitoring.MethodLifeCycleItems;

using System;
using System.Threading;
using Orc.Monitoring;

/// <summary>
/// Represents the end of a method call in the performance monitoring system.
/// </summary>
public class MethodCallEnd : IMethodLifeCycleItem
{
    public MethodCallEnd(MethodCallInfo methodCallInfo)
    {
        ArgumentNullException.ThrowIfNull(methodCallInfo);

        TimeStamp = methodCallInfo.StartTime + methodCallInfo.Elapsed;
        MethodCallInfo = methodCallInfo;
        ThreadId = Thread.CurrentThread.ManagedThreadId;
    }

    /// <summary>
    /// Gets the timestamp of the end of the method call.
    /// </summary>
    public DateTime TimeStamp { get; }

    /// <summary>
    /// Gets the context of the method call.
    /// </summary>
    public MethodCallInfo MethodCallInfo { get; }

    /// <summary>
    /// Gets the ID of the thread that ended the method call.
    /// </summary>
    public int ThreadId { get; }

    public override string ToString() => $"MethodCallEnd: {MethodCallInfo}";
}

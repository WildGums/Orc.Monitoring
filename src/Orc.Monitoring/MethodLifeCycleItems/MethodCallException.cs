namespace Orc.Monitoring.MethodLifeCycleItems;

using System;
using System.Threading;
using Monitoring;

/// <summary>
/// Represents an exception that occurred during a method call in the performance monitoring system.
/// </summary>
public class MethodCallException : IMethodLifeCycleItem
{
    public MethodCallException(MethodCallInfo methodCallInfo, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        MethodCallInfo = methodCallInfo;
        Exception = exception;
        ThreadId = Thread.CurrentThread.ManagedThreadId;
        TimeStamp = DateTime.Now;
    }

    /// <summary>
    /// Gets the timestamp of the exception.
    /// </summary>
    public DateTime TimeStamp { get; }

    public MethodCallInfo MethodCallInfo { get; }

    /// <summary>
    /// Gets the exception that occurred.
    /// </summary>
    public Exception Exception { get; }

    /// <summary>
    /// Gets the ID of the thread that was executing the method call.
    /// </summary>
    public int ThreadId { get; }

    public override string ToString()
    {
        var methodCallInfoString = MethodCallInfo?.ToString() ?? "null";
        return $"MethodCallException: {methodCallInfoString}";
    }
}

namespace Orc.Monitoring.Core.MethodLifecycle;

using System;
using Models;

/// <summary>
/// Represents an exception that occurred during a method call in the performance monitoring system.
/// </summary>
public class MethodCallException : MethodLifeCycleItemBase
{
    public MethodCallException(MethodCallInfo methodCallInfo, Exception exception)
    : base(methodCallInfo)
    {
        ArgumentNullException.ThrowIfNull(exception);

        ExceptionMessage = exception.Message;
        StackTrace = exception.StackTrace ?? string.Empty;

        TimeStamp = DateTime.Now;
    }

    public string ExceptionMessage { get; }
    public string StackTrace { get; }

    public override string ToString()
        => $"MethodCallException at {TimeStamp}: {ExceptionMessage} in {MethodCallInfo}";
}

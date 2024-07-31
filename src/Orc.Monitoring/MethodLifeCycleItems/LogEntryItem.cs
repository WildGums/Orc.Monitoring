namespace Orc.Monitoring.MethodLifeCycleItems;

using System;
using System.Threading;
using Orc.Monitoring;

/// <summary>
/// Represents a log entry in the performance monitoring system.
/// </summary>
public class LogEntryItem : IMethodLifeCycleItem
{
    public LogEntryItem(MethodCallInfo methodCallInfo, string category, object data)
    {
        ArgumentNullException.ThrowIfNull(methodCallInfo);
        ArgumentNullException.ThrowIfNull(category);
        ArgumentNullException.ThrowIfNull(data);

        MethodCallInfo = methodCallInfo;
        Category = category;
        Data = data;
        TimeStamp = DateTime.Now;
        ThreadId = Thread.CurrentThread.ManagedThreadId;
    }

    /// <summary>
    /// Gets the timestamp of the log entry.
    /// </summary>
    public DateTime TimeStamp { get; }

    public MethodCallInfo MethodCallInfo { get; }

    /// <summary>
    /// Gets the category of the log entry.
    /// </summary>
    public string Category { get; }

    /// <summary>
    /// Gets the data of the log entry.
    /// </summary>
    public object Data { get; }

    /// <summary>
    /// Gets the ID of the thread that made the log entry.
    /// </summary>
    public int ThreadId { get; }

    public override string ToString() => $"LogEntry: {Category} {MethodCallInfo}";
}


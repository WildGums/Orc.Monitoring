namespace Orc.Monitoring.MethodLifeCycleItems;

using System;
using System.Threading;
using Monitoring;

/// <summary>
/// Represents a log entry in the performance monitoring system.
/// </summary>
public class LogEntryItem<T> : MethodLifeCycleItemBase
{
    public LogEntryItem(MethodCallInfo methodCallInfo, string category, T data)
    : base(methodCallInfo)
    {
        ArgumentNullException.ThrowIfNull(category);
        ArgumentNullException.ThrowIfNull(data);

        Category = category;
        Data = data;
        TimeStamp = DateTime.Now;
    }

    /// <summary>
    /// Gets the category of the log entry.
    /// </summary>
    public string Category { get; }

    /// <summary>
    /// Gets the data of the log entry.
    /// </summary>
    public T Data { get; }

    public override string ToString() => $"LogEntryItem at {TimeStamp}: {Category} in {MethodCallInfo}";
}


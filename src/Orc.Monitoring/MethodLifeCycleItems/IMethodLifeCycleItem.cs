namespace Orc.Monitoring.MethodLifeCycleItems;

using System;
using Orc.Monitoring;

/// <summary>
/// Defines properties for an item in the lifecycle of a method call.
/// </summary>
public interface IMethodLifeCycleItem : ICallStackItem
{
    /// <summary>
    /// Gets the timestamp of the lifecycle event.
    /// </summary>
    DateTime TimeStamp { get; }

    /// <summary>
    /// Gets the context of the method call.
    /// </summary>
    MethodCallInfo MethodCallInfo { get; }

    /// <summary>
    /// Gets the ID of the thread that executed the method call.
    /// </summary>
    int ThreadId { get; }
}

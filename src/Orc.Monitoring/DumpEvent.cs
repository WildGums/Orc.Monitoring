namespace Orc.Monitoring;

using System;
using MethodLifeCycleItems;

/// <summary>
/// Represents the event data for a dump event.
/// </summary>
public class DumpEvent : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DumpEvent"/> class.
    /// </summary>
    /// <param name="methodLifeCycleItem">The lifecycle item of the method call.</param>
    public DumpEvent(IMethodLifeCycleItem methodLifeCycleItem)
    {
        MethodLifeCycleItem = methodLifeCycleItem;
    }

    /// <summary>
    /// Gets the lifecycle item of the method call.
    /// </summary>
    public IMethodLifeCycleItem MethodLifeCycleItem { get; }
}

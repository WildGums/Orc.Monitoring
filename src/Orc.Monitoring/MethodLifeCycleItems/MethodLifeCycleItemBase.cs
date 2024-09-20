namespace Orc.Monitoring.MethodLifeCycleItems;

using System;
using System.Threading;

public abstract class MethodLifeCycleItemBase : IMethodLifeCycleItem
{
    public DateTime TimeStamp { get; protected set; }
    public MethodCallInfo MethodCallInfo { get; protected set; }
    public int ThreadId { get; protected set; }

    protected MethodLifeCycleItemBase(MethodCallInfo methodCallInfo)
    {
        ArgumentNullException.ThrowIfNull(methodCallInfo);

        MethodCallInfo = methodCallInfo;
        ThreadId = Thread.CurrentThread.ManagedThreadId;
    }
}

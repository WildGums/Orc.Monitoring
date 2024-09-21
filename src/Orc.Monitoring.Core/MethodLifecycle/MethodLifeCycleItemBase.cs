namespace Orc.Monitoring.Core.MethodLifecycle;

using System;
using System.Threading;
using Models;

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

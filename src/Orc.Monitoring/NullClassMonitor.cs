namespace Orc.Monitoring;

using System;
using System.Runtime.CompilerServices;
using MethodLifeCycleItems;

public class NullClassMonitor : IClassMonitor
{
    public AsyncMethodCallContext StartAsyncMethod(MethodConfiguration config, string callerMethod = "")
    {
        Console.WriteLine($"NullClassMonitor.StartAsyncMethod called for {callerMethod}");
        return AsyncMethodCallContext.Dummy;
    }

    public MethodCallContext StartMethod(MethodConfiguration config, [CallerMemberName] string callerMethod = "")
    {
        Console.WriteLine($"NullClassMonitor.StartMethod called for {callerMethod}");
        return MethodCallContext.Dummy;
    }

    public void LogStatus(IMethodLifeCycleItem status)
    {
        Console.WriteLine($"NullClassMonitor.LogStatus called with {status.GetType().Name}");
    }
}

namespace Orc.Monitoring.MethodLifeCycleItems;

using System;
using System.Collections.Generic;

public class CallGap : ICallStackItem
{
    public CallGap(DateTime startTime, DateTime endTime)
    {
        TimeStamp = startTime;
        Elapsed = endTime - startTime;
    }
    public DateTime TimeStamp { get; }
    public TimeSpan Elapsed { get; }
    public Dictionary<string, string> Parameters { get; } = new();
    public override string ToString() => $"CallGap: {Elapsed}";
}

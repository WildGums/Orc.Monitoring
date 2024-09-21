namespace Orc.Monitoring.Core.MethodLifecycle;

using System;
using System.Collections.Generic;

public class CallGap(DateTime startTime, DateTime endTime) : ICallStackItem
{
    public DateTime TimeStamp { get; } = startTime;
    public TimeSpan Elapsed { get; } = endTime - startTime;
    public Dictionary<string, string> Parameters { get; } = new();
    public override string ToString() => $"CallGap: {Elapsed}";
}

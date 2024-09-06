namespace Orc.Monitoring.Examples.MonitoredClasses;

using System;
using Orc.Monitoring;
using Reporters;

public class SimpleClass
{
    private readonly IClassMonitor _monitor;

    public SimpleClass()
    {
        _monitor = Performance.Monitor.ForClass<SimpleClass>();
    }

    public void MonitoredMethod()
    {
        using var context = _monitor.Start(b => b.AddReporter(new WorkflowReporter()));
        Console.WriteLine("Executing MonitoredMethod");
        // Simulate some work
        System.Threading.Thread.Sleep(100);
    }

    public void MonitoredMethodWithParameters(int intParam, string stringParam)
    {
        using var context = _monitor.Start(b => b.AddReporter(new WorkflowReporter()));
        Console.WriteLine($"Executing MonitoredMethodWithParameters: {intParam}, {stringParam}");
        context.SetParameter("IntParam", intParam.ToString());
        context.SetParameter("StringParam", stringParam);
        // Simulate some work
        System.Threading.Thread.Sleep(200);
    }

    public void MonitoredMethodWithException()
    {
        using var context = _monitor.Start(b => b.AddReporter(new WorkflowReporter()));
        Console.WriteLine("Executing MonitoredMethodWithException");
        // Simulate some work before exception
        System.Threading.Thread.Sleep(50);
        throw new InvalidOperationException("This is a test exception");
    }
}

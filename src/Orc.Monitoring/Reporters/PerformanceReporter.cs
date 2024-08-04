namespace Orc.Monitoring.Reporters;

using System;
using System.Reflection;
using MethodLifeCycleItems;
using ReportOutputs;



public class PerformanceReporter : IMethodCallReporter
{
    public string Name => "PerformanceReporter";
    public string FullName => "Orc.Monitoring.Reporters.PerformanceReporter";
    public MethodInfo? RootMethod { get; set; }

    public IAsyncDisposable StartReporting(IObservable<ICallStackItem> callStack)
    {
        // In a real implementation, we would start measuring performance here
        Console.WriteLine("PerformanceReporter started reporting");

        return new AsyncDisposable(async () =>
        {
            // In a real implementation, we would stop measuring and report results here
            Console.WriteLine("PerformanceReporter stopped reporting");
        });
    }

    public IOutputContainer AddOutput<TOutput>(object? parameter = null) where TOutput : IReportOutput, new()
    {
        // In a real implementation, we would add an output here
        Console.WriteLine($"PerformanceReporter added output: {typeof(TOutput).Name}");
        return this;
    }
}

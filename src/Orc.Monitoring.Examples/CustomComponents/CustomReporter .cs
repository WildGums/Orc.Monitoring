namespace Orc.Monitoring.Examples.CustomComponents;

using System;
using System.Reflection;
using System.Threading.Tasks;
using Orc.Monitoring;
using Orc.Monitoring.Reporters;
using Orc.Monitoring.Reporters.ReportOutputs;
using Filters;
using MethodLifeCycleItems;

public class CustomReporter : IMethodCallReporter
{
    public string Name => "CustomReporter";
    public string FullName => "CustomReporter";
    public MethodInfo? RootMethod { get; private set; }
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public void Initialize(MonitoringConfiguration monitoringConfiguration, MethodCallInfo rootMethod)
    {
        ArgumentNullException.ThrowIfNull(monitoringConfiguration);
        ArgumentNullException.ThrowIfNull(rootMethod.MethodInfo);

        RootMethod = rootMethod.MethodInfo;
    }

    public IAsyncDisposable StartReporting(IObservable<ICallStackItem> callStack)
    {
        Console.WriteLine($"CustomReporter: Started reporting for {RootMethod?.Name}");

        return new AsyncDisposable(async () =>
        {
            await Task.Delay(10); // Simulate some async cleanup work
            Console.WriteLine($"CustomReporter: Finished reporting for {RootMethod?.Name}");
        });
    }

    public IOutputContainer AddOutput<TOutput>(object? parameter = null) where TOutput : IReportOutput, new()
    {
        // In this example, we're not implementing custom outputs
        return this;
    }

    public IOutputContainer AddFilter<T>() where T : IMethodFilter
    {
        // In this example, we're not implementing custom filters
        return this;
    }
}

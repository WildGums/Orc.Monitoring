namespace Orc.Monitoring.Examples.MonitoredClasses;

using System;
using Filters;
using Monitoring;
using Orc.Monitoring.Reporters.ReportOutputs;
using Reporters;

public class SimpleClass
{
    private readonly IClassMonitor _monitor;

    public SimpleClass()
    {
        _monitor = Performance.Monitor.ForClass<SimpleClass>();
    }

    [MethodCallParameter(MethodCallParameter.WorkflowItemName, "MonitoredMethod")]
    public void MonitoredMethod()
    {
        var logFolder = "C:/Temp";

        using var context = _monitor.Start(config => config
            .AddReporter<WorkflowReporter>(x => x
                .AddFilter<WorkflowItemFilter>()
                .AddFilter<WorkflowItemGranularityFilter>()
                .AddOutput<TxtReportOutput>(TxtReportOutput.CreateParameters(logFolder, MethodCallParameter.WorkflowItemName))
                .AddOutput<CsvReportOutput>(CsvReportOutput.CreateParameters(logFolder, "MonitoredMethod.csv"))));
        Console.WriteLine("Executing MonitoredMethod");
        // Simulate some work
        Thread.Sleep(100);
    }

    [MethodCallParameter(MethodCallParameter.WorkflowItemName, "MonitoredMethodWithParameters")]
    public void MonitoredMethodWithParameters(int intParam, string stringParam)
    {
        var logFolder = "C:/Temp";

        using var context = _monitor.Start(config => config
            .AddReporter<WorkflowReporter>(x => x
                .AddFilter<WorkflowItemFilter>()
                .AddFilter<WorkflowItemGranularityFilter>()
                .AddOutput<TxtReportOutput>(TxtReportOutput.CreateParameters(logFolder, MethodCallParameter.WorkflowItemName))
                .AddOutput<CsvReportOutput>(CsvReportOutput.CreateParameters(logFolder, "MonitoredMethodWithParameters.csv"))));
        Console.WriteLine($"Executing MonitoredMethodWithParameters: {intParam}, {stringParam}");
        context.SetParameter("IntParam", intParam.ToString());
        context.SetParameter("StringParam", stringParam);
        // Simulate some work
        Thread.Sleep(200);
    }

    [MethodCallParameter(MethodCallParameter.WorkflowItemName, "MonitoredMethodWithException")]
    public void MonitoredMethodWithException()
    {
        var logFolder = "C:/Temp";

        using var context = _monitor.Start(config => config
            .AddReporter<WorkflowReporter>(x => x
                .AddFilter<WorkflowItemFilter>()
                .AddFilter<WorkflowItemGranularityFilter>()
                .AddOutput<TxtReportOutput>(TxtReportOutput.CreateParameters(logFolder, MethodCallParameter.WorkflowItemName))
                .AddOutput<CsvReportOutput>(CsvReportOutput.CreateParameters(logFolder, "MonitoredMethodWithException.csv"))));
        Console.WriteLine("Executing MonitoredMethodWithException");
        // Simulate some work before exception
        Thread.Sleep(50);
        throw new InvalidOperationException("This is a test exception");
    }
}

﻿namespace Orc.Monitoring.Examples.MonitoredClasses;

using System;
using System.Threading.Tasks;
using Monitoring;
using Orc.Monitoring.Filters;
using Orc.Monitoring.Reporters.ReportOutputs;
using Reporters;


public class ComplexWorkflow
{
    private readonly IClassMonitor _monitor;

    public ComplexWorkflow()
    {
        _monitor = Performance.Monitor.ForClass<ComplexWorkflow>();
    }

    [MethodCallParameter(MethodCallParameter.WorkflowItemName, "ComplexWorkflow")]
    [MethodCallParameter(MethodCallParameter.WorkflowItemType, MethodCallParameter.Types.DataProcess)]
    [MethodCallParameter(MethodCallParameter.WorkflowItemGranularity, MethodCallParameter.Granularity.Coarse)]

    public async Task ExecuteWorkflowAsync()
    {
        var logFolder = "C:/Temp";

        await using var context = _monitor.AsyncStart(config => config
            .AddReporter<WorkflowReporter>(x => x
                .AddFilter<WorkflowItemFilter>()
                .AddFilter<WorkflowItemGranularityFilter>()
                .AddOutput<TxtReportOutput>(TxtReportOutput.CreateParameters(logFolder, MethodCallParameter.WorkflowItemName))
                .AddOutput<CsvReportOutput>(CsvReportOutput.CreateParameters(logFolder, "ComplexWorkflow.csv"))));

        Console.WriteLine("Starting Complex Workflow");

        await Step1Async();
        await Step2Async();
        await Step3Async();

        Console.WriteLine("Complex Workflow Completed");
    }

    [MethodCallParameter(MethodCallParameter.WorkflowItemName, "Step1")]
    [MethodCallParameter(MethodCallParameter.WorkflowItemGranularity, MethodCallParameter.Granularity.Medium)]
    private async Task Step1Async()
    {
        await using var context = _monitor.AsyncStart(_ => { });

        Console.WriteLine("Executing Step 1");
        await Task.Delay(300); // Simulate work
    }

    [MethodCallParameter(MethodCallParameter.WorkflowItemName, "Step2")]
    [MethodCallParameter(MethodCallParameter.WorkflowItemGranularity, MethodCallParameter.Granularity.Medium)]
    private async Task Step2Async()
    {
        await using var context = _monitor.AsyncStart(_ => { });

        Console.WriteLine("Executing Step 2");
        await Task.Delay(500); // Simulate work
        await Step2SubTaskAsync();
    }

    [MethodCallParameter(MethodCallParameter.WorkflowItemName, "Step2SubTask")]
    [MethodCallParameter(MethodCallParameter.WorkflowItemGranularity, MethodCallParameter.Granularity.Fine)]
    private async Task Step2SubTaskAsync()
    {
        await using var context = _monitor.AsyncStart(_ => { });

        Console.WriteLine("Executing Step 2 Sub-Task");
        await Task.Delay(200); // Simulate work
    }

    [MethodCallParameter(MethodCallParameter.WorkflowItemName, "Step3")]
    [MethodCallParameter(MethodCallParameter.WorkflowItemGranularity, MethodCallParameter.Granularity.Medium)]
    private async Task Step3Async()
    {
        await using var context = _monitor.AsyncStart(_ => { });

        Console.WriteLine("Executing Step 3");
        await Task.Delay(400); // Simulate work
    }
}

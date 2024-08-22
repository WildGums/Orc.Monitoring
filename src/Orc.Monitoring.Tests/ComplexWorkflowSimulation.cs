﻿namespace Orc.Monitoring.Tests;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Reporters;
using Reporters.ReportOutputs;

public class ComplexWorkflowSimulation
{
    private readonly IClassMonitor _monitor;
    private readonly Random _random = new Random();
    private readonly string _outputFolder;

    public TimeSpan MainProcessDuration { get; private set; }
    public int SubProcessCount { get; private set; }
    public int AsyncOperationCount { get; private set; }
    public int ExceptionCount { get; private set; }

    public ComplexWorkflowSimulation(string outputFolder)
    {
        _monitor = PerformanceMonitor.ForClass<ComplexWorkflowSimulation>();
        _outputFolder = outputFolder;
    }

    public async Task RunWorkflowAsync()
    {
        await using var context = _monitor.AsyncStart(builder =>
        {
            builder.AddReporter<WorkflowReporter>(reporter =>
            {
                reporter.AddOutput<RanttOutput>(RanttOutput.CreateParameters(_outputFolder));
                reporter.AddOutput<CsvReportOutput>(CsvReportOutput.CreateParameters(_outputFolder, "WorkflowReport"));
            });
        });

        context.SetParameter(MethodCallParameter.WorkflowItemName, "MainProcess");
        context.SetParameter(MethodCallParameter.WorkflowItemType, MethodCallParameter.Types.Overview);

        var startTime = DateTime.Now;

        try
        {
            // Execute each task sequentially for debugging
            await RunSubProcessesAsync();
            await RunAsyncOperationsAsync();
            await SimulateExceptionsAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during workflow execution: {ex}");
            context.LogException(ex);
        }
        finally
        {
            MainProcessDuration = DateTime.Now - startTime;
        }
    }

    private async Task RunSubProcessesAsync()
    {
        for (int i = 0; i < 5; i++)
        {
            using (var context = _monitor.Start(builder => builder.AddReporter<WorkflowReporter>()))
            {
                context.SetParameter(MethodCallParameter.WorkflowItemName, $"SubProcess_{i}");
                context.SetParameter(MethodCallParameter.WorkflowItemType, MethodCallParameter.Types.DataProcess);

                await Task.Delay(_random.Next(100, 500));
                SubProcessCount++;
                Console.WriteLine($"SubProcess_{i} completed");
            }
        }
    }

    private async Task RunAsyncOperationsAsync()
    {
        for (int i = 0; i < 3; i++)
        {
            await using (var context = _monitor.StartAsyncMethod(new MethodConfiguration
            {
                Reporters = new List<IMethodCallReporter> { new WorkflowReporter() }
            }))
            {
                context.SetParameter(MethodCallParameter.WorkflowItemName, $"AsyncOperation_{i}");
                context.SetParameter(MethodCallParameter.WorkflowItemType, MethodCallParameter.Types.DataIo);

                await Task.Delay(_random.Next(200, 800));
                AsyncOperationCount++;
            }
        }
    }

    private async Task SimulateExceptionsAsync()
    {
        for (int i = 0; i < 2; i++)
        {
            using (var context = _monitor.Start(builder => builder.AddReporter<WorkflowReporter>()))
            {
                context.SetParameter(MethodCallParameter.WorkflowItemName, $"ExceptionSimulation_{i}");
                context.SetParameter(MethodCallParameter.WorkflowItemType, MethodCallParameter.Types.DataProcess);

                await Task.Delay(_random.Next(50, 150));
                try
                {
                    throw new Exception($"Simulated exception {i}");
                }
                catch (Exception ex)
                {
                    context.LogException(ex);
                    ExceptionCount++;
                }
            }
        }
    }
}

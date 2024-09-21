namespace Orc.Monitoring.Examples.MonitoredClasses;

using Core.Abstractions;
using Core.Attributes;
using Core.Extensions;
using Core.PerformanceMonitoring;
using Orc.Monitoring.Filters;
using Orc.Monitoring.Reporters.ReportOutputs;
using Reporters;


public class AsyncOperations
{
    private readonly IClassMonitor _monitor;

    public AsyncOperations()
    {
        _monitor = Performance.Monitor.ForClass<AsyncOperations>();
    }

    [MethodCallParameter(MethodCallParameter.WorkflowItemName, "MonitoredAsyncMethod")]
    public async Task MonitoredAsyncMethodAsync()
    {
        var logFolder = "C:/Temp";

        await using var context = _monitor.AsyncStart(config => config
            .AddReporter<WorkflowReporter>(x => x
                .AddFilter<WorkflowItemFilter>()
                .AddFilter<WorkflowItemGranularityFilter>()
                .AddOutput<TxtReportOutput>(TxtReportOutput.CreateParameters(logFolder, MethodCallParameter.WorkflowItemName))
                .AddOutput<CsvReportOutput>(CsvReportOutput.CreateParameters(logFolder, "MonitoredAsyncMethod.csv"))));

        Console.WriteLine("Starting MonitoredAsyncMethod");
        await Task.Delay(1000); // Simulate async work
        await NestedAsyncMethodAsync();
        Console.WriteLine("MonitoredAsyncMethod Completed");
    }

    [MethodCallParameter(MethodCallParameter.WorkflowItemName, "NestedAsyncMethod")]
    private async Task NestedAsyncMethodAsync()
    {
        await using var context = _monitor.AsyncStart(_ => { });

        Console.WriteLine("Executing NestedAsyncMethod");
        await Task.Delay(500); // Simulate async work
    }

    [MethodCallParameter(MethodCallParameter.WorkflowItemName, "MonitoredParallelOperations")]
    public async Task MonitoredParallelOperationsAsync()
    {
        var logFolder = "C:/Temp";

        await using var context = _monitor.AsyncStart(config => config
            .AddReporter<WorkflowReporter>(x => x
                .AddFilter<WorkflowItemFilter>()
                .AddFilter<WorkflowItemGranularityFilter>()
                .AddOutput<TxtReportOutput>(TxtReportOutput.CreateParameters(logFolder, MethodCallParameter.WorkflowItemName))
                .AddOutput<CsvReportOutput>(CsvReportOutput.CreateParameters(logFolder, "MonitoredParallelOperations.csv"))));

        Console.WriteLine("Starting MonitoredParallelOperations");

        var tasks = new Task[3];
        for (int i = 0; i < 3; i++)
        {
            int taskId = i;
            tasks[i] = Task.Run(async () => await ParallelTaskAsync(taskId));
        }

        await Task.WhenAll(tasks);

        Console.WriteLine("MonitoredParallelOperations Completed");
    }

    [MethodCallParameter(MethodCallParameter.WorkflowItemName, "ParallelTask")]
    private async Task ParallelTaskAsync(int taskId)
    {
        await using var context = _monitor.AsyncStart(_ => { });

        Console.WriteLine($"Executing ParallelTask {taskId}");
        await Task.Delay(300 * (taskId + 1)); // Simulate varying workloads
    }
}

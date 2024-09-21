namespace Orc.Monitoring.Examples;

using System;
using System.Threading.Tasks;
using Core.PerformanceMonitoring;
using Monitoring;
using MonitoredClasses;
using Reporters.ReportOutputs;
using Orc.Monitoring.Filters;
using Orc.Monitoring.Reporters;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Orc.Monitoring Example Application");
        Console.WriteLine("----------------------------------");

        // Configure PerformanceMonitor
        Performance.Monitor.Configure(conf =>
        {
            conf.AddReporterType<WorkflowReporter>()
                .AddFilter<WorkflowItemFilter>()
                .AddFilter(new WorkflowItemGranularityFilter(MethodCallParameter.Granularity.Fine))
                .TrackAssembly(typeof(ComplexWorkflow).Assembly);
        });

        Performance.Controller.Enable();

        Performance.Controller.EnableFilter(typeof(WorkflowItemFilter));
        Performance.Controller.EnableFilter(typeof(WorkflowItemGranularityFilter));

        Performance.Controller.EnableFilterForReporterType(typeof(WorkflowReporter), typeof(WorkflowItemFilter));
        Performance.Controller.EnableFilterForReporterType(typeof(WorkflowReporter), typeof(WorkflowItemGranularityFilter));

        Performance.Controller.EnableOutputType<CsvReportOutput>();
        Performance.Controller.EnableOutputType<TxtReportOutput>();

        Performance.Controller.EnableReporter(typeof(WorkflowReporter));

        Console.WriteLine("Monitoring configured. Starting demonstrations...");

        // Simple monitoring
        Console.WriteLine("\nDemonstrating Simple Class Monitoring:");
        var simpleClass = new SimpleClass();
        simpleClass.MonitoredMethod();
        simpleClass.MonitoredMethodWithParameters(42, "test");
        try
        {
            simpleClass.MonitoredMethodWithException();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Caught expected exception: {ex.Message}");
        }

        // Complex workflow
        Console.WriteLine("\nDemonstrating Complex Workflow Monitoring:");
        var complexWorkflow = new ComplexWorkflow();
        await complexWorkflow.ExecuteWorkflowAsync();

        // Async operations
        Console.WriteLine("\nDemonstrating Async Operations Monitoring:");
        var asyncOps = new AsyncOperations();
        await asyncOps.MonitoredAsyncMethodAsync();
        await asyncOps.MonitoredParallelOperationsAsync();

        Console.WriteLine("\nMonitoring demonstration completed.");
        Console.WriteLine("Check the MonitoringOutput folder for generated reports.");
    }
}

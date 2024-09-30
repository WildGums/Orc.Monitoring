namespace Orc.Monitoring.Examples;

using System;
using System.Threading.Tasks;
using Core.Controllers;
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

        Performance.Controller.Enable();

        Performance.Controller.EnableFilter(typeof(WorkflowItemFilter));
        Performance.Controller.EnableFilter(typeof(WorkflowItemGranularityFilter));

        Performance.Controller.EnableFilterForReporter(typeof(WorkflowReporter), typeof(WorkflowItemFilter));
        Performance.Controller.EnableFilterForReporter(typeof(WorkflowReporter), typeof(WorkflowItemGranularityFilter));

        Performance.Controller.EnableOutput<CsvReportOutput>();
        Performance.Controller.EnableOutput<TxtReportOutput>();

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

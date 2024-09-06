using System;
using System.Threading.Tasks;
using Orc.Monitoring;
using Orc.Monitoring.Examples.MonitoredClasses;
using Orc.Monitoring.Examples.CustomComponents;
using Orc.Monitoring.Reporters.ReportOutputs;
using System.ComponentModel;

namespace Orc.Monitoring.Examples
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Orc.Monitoring Example Application");
            Console.WriteLine("----------------------------------");

            // Configure PerformanceMonitor
            Performance.Configure(config =>
            {
                config.AddReporterType<CsvReportOutput>()
                      .AddReporterType<TxtReportOutput>()
                      .AddReporterType<RanttOutput>()
                      .AddReporterType<CustomReporter>()
                      .AddFilter<CustomFilter>()
                      .SetGlobalState(true);

                // Set output folder for reports
                var outputFolder = Path.Combine(Environment.CurrentDirectory, "MonitoringOutput");
                config.SetOutputTypeState<CsvReportOutput>(true);
                config.SetOutputTypeState<TxtReportOutput>(true);
                config.SetOutputTypeState<RanttOutput>(true);

                var csvParams = CsvReportOutput.CreateParameters(outputFolder, "ExampleReport");
                var txtParams = TxtReportOutput.CreateParameters(outputFolder, "WorkflowName");
                var ranttParams = RanttOutput.CreateParameters(outputFolder);
            });

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
}

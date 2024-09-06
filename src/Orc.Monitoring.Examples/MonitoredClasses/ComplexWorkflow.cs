using System;
using System.Threading.Tasks;
using Orc.Monitoring;
using Orc.Monitoring.Reporters;

namespace Orc.Monitoring.Examples.MonitoredClasses
{
    public class ComplexWorkflow
    {
        private readonly IClassMonitor _monitor;

        public ComplexWorkflow()
        {
            _monitor = Performance.Monitor.ForClass<ComplexWorkflow>();
        }

        public async Task ExecuteWorkflowAsync()
        {
            await using var context = _monitor.AsyncStart(b => b.AddReporter(new WorkflowReporter()));
            context.SetParameter(MethodCallParameter.WorkflowItemName, "ComplexWorkflow");
            context.SetParameter(MethodCallParameter.WorkflowItemType, MethodCallParameter.Types.DataProcess);

            Console.WriteLine("Starting Complex Workflow");

            await Step1Async();
            await Step2Async();
            await Step3Async();

            Console.WriteLine("Complex Workflow Completed");
        }

        private async Task Step1Async()
        {
            await using var context = _monitor.AsyncStart(b => b.AddReporter(new WorkflowReporter()));
            context.SetParameter(MethodCallParameter.WorkflowItemName, "Step1");
            context.SetParameter(MethodCallParameter.WorkflowItemGranularity, MethodCallParameter.Granularity.Coarse);

            Console.WriteLine("Executing Step 1");
            await Task.Delay(300); // Simulate work
        }

        private async Task Step2Async()
        {
            await using var context = _monitor.AsyncStart(b => b.AddReporter(new WorkflowReporter()));
            context.SetParameter(MethodCallParameter.WorkflowItemName, "Step2");
            context.SetParameter(MethodCallParameter.WorkflowItemGranularity, MethodCallParameter.Granularity.Medium);

            Console.WriteLine("Executing Step 2");
            await Task.Delay(500); // Simulate work
            await Step2SubTaskAsync();
        }

        private async Task Step2SubTaskAsync()
        {
            await using var context = _monitor.AsyncStart(b => b.AddReporter(new WorkflowReporter()));
            context.SetParameter(MethodCallParameter.WorkflowItemName, "Step2SubTask");
            context.SetParameter(MethodCallParameter.WorkflowItemGranularity, MethodCallParameter.Granularity.Fine);

            Console.WriteLine("Executing Step 2 Sub-Task");
            await Task.Delay(200); // Simulate work
        }

        private async Task Step3Async()
        {
            await using var context = _monitor.AsyncStart(b => b.AddReporter(new WorkflowReporter()));
            context.SetParameter(MethodCallParameter.WorkflowItemName, "Step3");
            context.SetParameter(MethodCallParameter.WorkflowItemGranularity, MethodCallParameter.Granularity.Coarse);

            Console.WriteLine("Executing Step 3");
            await Task.Delay(400); // Simulate work
        }
    }
}

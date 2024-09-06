using System;
using System.Threading.Tasks;
using Orc.Monitoring;
using Orc.Monitoring.Reporters;

namespace Orc.Monitoring.Examples.MonitoredClasses
{
    public class AsyncOperations
    {
        private readonly IClassMonitor _monitor;

        public AsyncOperations()
        {
            _monitor = Performance.Monitor.ForClass<AsyncOperations>();
        }

        public async Task MonitoredAsyncMethodAsync()
        {
            await using var context = _monitor.AsyncStart(b => b.AddReporter(new WorkflowReporter()));
            context.SetParameter(MethodCallParameter.WorkflowItemName, "MonitoredAsyncMethod");

            Console.WriteLine("Starting MonitoredAsyncMethod");
            await Task.Delay(1000); // Simulate async work
            await NestedAsyncMethodAsync();
            Console.WriteLine("MonitoredAsyncMethod Completed");
        }

        private async Task NestedAsyncMethodAsync()
        {
            await using var context = _monitor.AsyncStart(b => b.AddReporter(new WorkflowReporter()));
            context.SetParameter(MethodCallParameter.WorkflowItemName, "NestedAsyncMethod");

            Console.WriteLine("Executing NestedAsyncMethod");
            await Task.Delay(500); // Simulate async work
        }

        public async Task MonitoredParallelOperationsAsync()
        {
            await using var context = _monitor.AsyncStart(b => b.AddReporter(new WorkflowReporter()));
            context.SetParameter(MethodCallParameter.WorkflowItemName, "MonitoredParallelOperations");

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

        private async Task ParallelTaskAsync(int taskId)
        {
            await using var context = _monitor.AsyncStart(b => b.AddReporter(new WorkflowReporter()));
            context.SetParameter(MethodCallParameter.WorkflowItemName, $"ParallelTask_{taskId}");

            Console.WriteLine($"Executing ParallelTask {taskId}");
            await Task.Delay(300 * (taskId + 1)); // Simulate varying workloads
        }
    }
}

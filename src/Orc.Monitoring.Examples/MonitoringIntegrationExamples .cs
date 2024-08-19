namespace Orc.Monitoring.Examples;

using System;
using System.Threading.Tasks;
using Monitoring;
using Reporters;
using Filters;
using MethodLifeCycleItems;


/// <summary>
/// Provides examples of integrating various monitoring components.
/// </summary>
public class MonitoringIntegrationExamples
{
    /// <summary>
    /// Demonstrates integration with PerformanceMonitor, including error handling and custom logging.
    /// </summary>
    public static void PerformanceMonitorIntegration()
    {
        Console.WriteLine("Configuring PerformanceMonitor...");
        PerformanceMonitor.Configure(config =>
        {
            config.AddReporter<WorkflowReporter>();
            config.AddFilter<WorkflowItemFilter>();
            config.AddFilter<PerformanceFilter>();
            config.TrackAssembly(typeof(MonitoringIntegrationExamples).Assembly);
        });

        Console.WriteLine("Enabling monitoring...");
        MonitoringController.Enable();
        MonitoringController.EnableReporter(typeof(WorkflowReporter));
        MonitoringController.EnableFilter(typeof(WorkflowItemFilter));
        MonitoringController.EnableFilter(typeof(PerformanceFilter));

        Console.WriteLine("Using PerformanceMonitor...");
        var monitor = PerformanceMonitor.ForCurrentClass();
        using (var context = monitor.Start(builder =>
                   builder.AddReporter<WorkflowReporter>()))
        {
            try
            {
                Console.WriteLine("Performing monitored work");
                SimulateWork(500);

                // Log custom data
                context.Log("CustomCategory", "Work completed successfully");

                // Simulate an exception
                if (DateTime.Now.Ticks % 2 == 0)
                {
                    throw new Exception("Simulated error in monitored work");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error occurred: {ex.Message}");
                context.LogException(ex);
            }
        }

        Console.WriteLine("Disabling monitoring...");
        MonitoringController.Disable();
    }

    /// <summary>
    /// Demonstrates async integration with CallStack, including nested method calls.
    /// </summary>
    public static async Task CallStackIntegrationAsync()
    {
        Console.WriteLine("Enabling monitoring...");
        MonitoringController.Enable();
        var callStack = new CallStack(new MonitoringConfiguration());
        var observer = new CallStackObserver();
        var classMonitor = PerformanceMonitor.ForClass<MonitoringIntegrationExamples>();

        using (callStack.Subscribe(observer))
        {
            var config = new MethodCallContextConfig
            {
                ClassType = typeof(MonitoringIntegrationExamples),
                CallerMethodName = nameof(CallStackIntegrationAsync)
            };

            var methodCallInfo = callStack.CreateMethodCallInfo(classMonitor, typeof(MonitoringIntegrationExamples), config);
            callStack.Push(methodCallInfo);
            Console.WriteLine("Root method started");

            await SimulateWorkAsync(200);
            await NestedMethodAsync(callStack, classMonitor);

            MonitoringController.Disable();

            callStack.Pop(methodCallInfo);
            Console.WriteLine("Root method ended");
        }

        Console.WriteLine("CallStack items received:");
        foreach (var item in observer.ReceivedItems)
        {
            Console.WriteLine($"  {item}");
        }
    }

    private static async Task NestedMethodAsync(CallStack callStack, IClassMonitor classMonitor)
    {
        var config = new MethodCallContextConfig
        {
            ClassType = typeof(MonitoringIntegrationExamples),
            CallerMethodName = nameof(NestedMethodAsync)
        };

        var methodCallInfo = callStack.CreateMethodCallInfo(classMonitor, typeof(MonitoringIntegrationExamples), config);
        callStack.Push(methodCallInfo);
        Console.WriteLine("Nested method started");

        await SimulateWorkAsync(100);

        callStack.Pop(methodCallInfo);
        Console.WriteLine("Nested method ended");
    }

    /// <summary>
    /// Demonstrates integration with ClassMonitor in a more complex scenario.
    /// </summary>
    public static void ComplexClassMonitorIntegration()
    {
        Console.WriteLine("Enabling monitoring...");
        MonitoringController.Enable();
        MonitoringController.EnableReporter(typeof(WorkflowReporter)); 

        var monitor = PerformanceMonitor.ForClass<MonitoringIntegrationExamples>();

        using (var context = monitor.Start(builder =>
                   builder.AddReporter<WorkflowReporter>()
                       .WithArguments("ComplexScenario", DateTime.Now)))
        {
            Console.WriteLine("Starting complex monitored workflow");

            // Simulate a multi-step process
            SimulateWork(300);
            context.Log("Step", "Initial processing completed");

            using (var subContext = monitor.Start(builder => builder.AddReporter<WorkflowReporter>()))
            {
                SimulateWork(200);
                subContext.Log("SubStep", "Detailed calculation performed");
            }

            SimulateWork(100);
            context.Log("Step", "Final processing completed");

            Console.WriteLine("Complex monitored workflow finished");
        }

        Console.WriteLine("Disabling monitoring...");
        MonitoringController.Disable();
    }

    private static void SimulateWork(int milliseconds)
    {
        Console.WriteLine($"Simulating work for {milliseconds}ms");
        Task.Delay(milliseconds).Wait();
    }

    private static async Task SimulateWorkAsync(int milliseconds)
    {
        Console.WriteLine($"Simulating async work for {milliseconds}ms");
        await Task.Delay(milliseconds);
    }
}

public class CallStackObserver : IObserver<ICallStackItem>
{
    public List<ICallStackItem> ReceivedItems { get; } = [];

    public void OnCompleted() => Console.WriteLine("CallStack observation completed");
    public void OnError(Exception error) => Console.WriteLine($"CallStack error: {error.Message}");
    public void OnNext(ICallStackItem value)
    {
        Console.WriteLine($"CallStack item received: {value}");
        ReceivedItems.Add(value);
    }
}

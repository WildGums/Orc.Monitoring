#pragma warning disable CL0002
namespace Orc.Monitoring.Benchmarks;

using BenchmarkDotNet.Attributes;
using System;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Core.Abstractions;
using Core.CallStacks;
using Core.Configuration;
using Core.Controllers;
using Core.MethodCallContexts;
using Core.Models;
using Core.Monitors;
using Core.PerformanceMonitoring;
using Core.Pooling;
using Reporters;
using Filters;
using Utilities.Logging;

[MemoryDiagnoser]
public class ConcurrencyBenchmarks
{
    private IMonitoringController? _monitoringController;
    private IPerformanceMonitor? _performanceMonitor;
    private CallStack? _callStack;
    private MethodCallInfoPool? _methodCallInfoPool;

    [GlobalSetup]
    public void Setup()
    {
        var loggerFactory = MonitoringLoggerFactory.Instance;
        _monitoringController = new MonitoringController(loggerFactory);
        _methodCallInfoPool = new MethodCallInfoPool(_monitoringController, loggerFactory);
        var methodCallContextFactory = new MethodCallContextFactory(_monitoringController, loggerFactory, _methodCallInfoPool);

        var classMonitorFactory = new ClassMonitorFactory(_monitoringController, loggerFactory, methodCallContextFactory, _methodCallInfoPool);
        var callStackFactory = new CallStackFactory(_monitoringController, loggerFactory, _methodCallInfoPool);

        _performanceMonitor = new PerformanceMonitor(_monitoringController, loggerFactory,
            callStackFactory,
            classMonitorFactory);

        _callStack = callStackFactory.CreateCallStack();

        _monitoringController.Enable();
    }

    [Benchmark]
    public async Task ConcurrentMethodMonitoringBenchmark()
    {
        const int concurrentOperations = 1000;
        var tasks = new Task[concurrentOperations];

        for (int i = 0; i < concurrentOperations; i++)
        {
            tasks[i] = Task.Run(async () =>
            {
                var monitor = _performanceMonitor!.ForClass<ConcurrencyBenchmarks>();
                await using var context = monitor.AsyncStart(builder => builder.AddReporter(new WorkflowReporter()));
                await Task.Delay(1); // Simulate some work
            });
        }

        await Task.WhenAll(tasks);
    }

    [Benchmark]
    public async Task ConcurrentCallStackOperationsBenchmark()
    {
        const int concurrentOperations = 1000;
        var tasks = new Task[concurrentOperations];

        for (int i = 0; i < concurrentOperations; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                var methodInfo = CreateMethodCallInfo($"Method{i}");
                _callStack!.Push(methodInfo);
                Task.Delay(1).Wait(); // Simulate some work
                _callStack.Pop(methodInfo);
            });
        }

        await Task.WhenAll(tasks);
    }

    [Benchmark]
    public async Task ConcurrentReporterStateChangesBenchmark()
    {
        const int concurrentOperations = 1000;
        var tasks = new Task[concurrentOperations];

        for (int i = 0; i < concurrentOperations; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                if (i % 2 == 0)
                    _monitoringController!.EnableReporter(typeof(WorkflowReporter));
                else
                    _monitoringController!.DisableReporter(typeof(WorkflowReporter));
            });
        }

        await Task.WhenAll(tasks);
    }

    [Benchmark]
    public async Task ConcurrentVersionChangesBenchmark()
    {
        const int concurrentOperations = 1000;
        var versions = new ConcurrentBag<MonitoringVersion>();

        var tasks = Enumerable.Range(0, concurrentOperations).Select(_ => Task.Run(() =>
        {
            _monitoringController!.EnableReporter(typeof(WorkflowReporter));
            versions.Add(_monitoringController!.GetCurrentVersion());
            _monitoringController.DisableReporter(typeof(WorkflowReporter));
        })).ToArray();

        await Task.WhenAll(tasks);

        // Ensure all versions are unique
        var uniqueVersions = new HashSet<MonitoringVersion>(versions);
        if (uniqueVersions.Count != versions.Count)
        {
            throw new InvalidOperationException("Not all versions are unique");
        }
    }

    private MethodCallInfo CreateMethodCallInfo(string methodName)
    {
        var config = new MethodCallContextConfig
        {
            ClassType = typeof(ConcurrencyBenchmarks),
            CallerMethodName = methodName
        };

        return _callStack!.CreateMethodCallInfo(null, typeof(ConcurrencyBenchmarks), config);
    }
}

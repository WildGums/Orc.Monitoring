#pragma warning disable CL0002
namespace Orc.Monitoring.Benchmarks;

using BenchmarkDotNet.Attributes;
using System;
using System.Threading.Tasks;
using Reporters;
using System.Collections.Generic;
using Core.Abstractions;
using Core.CallStacks;
using Core.Configuration;
using Core.Controllers;
using Core.Logging;
using Core.MethodCallContexts;
using Core.Monitors;
using Core.PerformanceMonitoring;
using Core.Pooling;
using Orc.Monitoring.Core.Utilities;

[MemoryDiagnoser]
public class AsyncOperationBenchmarks
{
    private IMonitoringController? _monitoringController;
    private IClassMonitor? _classMonitor;
    private MethodCallInfoPool? _methodCallInfoPool;
    private MethodCallContextFactory? _methodCallContextFactory;

    [GlobalSetup]
    public void Setup()
    {
        var loggerFactory = MonitoringLoggerFactory.Instance;
        _monitoringController = new MonitoringController(loggerFactory);
        _methodCallInfoPool = new MethodCallInfoPool(_monitoringController, loggerFactory);
        _methodCallContextFactory = new MethodCallContextFactory(_monitoringController, loggerFactory, _methodCallInfoPool);

        var callStackFactory = new CallStackFactory(_monitoringController, loggerFactory, _methodCallInfoPool);
        var classMonitorFactory = new ClassMonitorFactory(_monitoringController, loggerFactory, _methodCallContextFactory, _methodCallInfoPool);

        var performanceMonitor = new PerformanceMonitor(_monitoringController, loggerFactory,
            callStackFactory,
            classMonitorFactory,
            () => new ConfigurationBuilder(_monitoringController));

        performanceMonitor.Configure(config =>
        {
            config.AddReporterType<WorkflowReporter>();
            config.TrackAssembly(typeof(AsyncOperationBenchmarks).Assembly);
        });

        _classMonitor = performanceMonitor.ForClass<AsyncOperationBenchmarks>();
        _monitoringController.Enable();
    }

    [Benchmark]
    public async Task AsyncMethodCallContextCreationAndDisposalBenchmark()
    {
        await using var context = _classMonitor!.AsyncStart(builder => builder.AddReporter(new WorkflowReporter()));
        // Simulate some async work
        await Task.Delay(1);
    }

    [Benchmark]
    public async Task AsyncMethodCallContextWithExceptionBenchmark()
    {
        try
        {
            await using var context = _classMonitor!.AsyncStart(builder => builder.AddReporter(new WorkflowReporter()));
            // Simulate some async work that throws an exception
            await Task.Delay(1);
            throw new Exception("Test exception");
        }
        catch
        {
            // Exception is expected, do nothing
        }
    }

    [Benchmark]
    public async Task AsyncMethodCallContextWithParametersBenchmark()
    {
        await using var context = _classMonitor!.AsyncStart(builder =>
            builder.AddReporter(new WorkflowReporter())
                   .WithArguments("param1", 42));
        // Simulate some async work
        await Task.Delay(1);
    }

    [Benchmark]
    public async Task ConcurrentAsyncOperationsBenchmark()
    {
        var tasks = new List<Task>();
        for (int i = 0; i < 100; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                await using var context = _classMonitor!.AsyncStart(builder => builder.AddReporter(new WorkflowReporter()));
                // Simulate some async work
                await Task.Delay(1);
            }));
        }
        await Task.WhenAll(tasks);
    }

    [Benchmark]
    public async Task NestedAsyncOperationsBenchmark()
    {
        await using var outerContext = _classMonitor!.AsyncStart(builder => builder.AddReporter(new WorkflowReporter()));
        // Simulate some async work
        await Task.Delay(1);

        await using var innerContext = _classMonitor!.AsyncStart(builder => builder.AddReporter(new WorkflowReporter()));
        // Simulate some more async work
        await Task.Delay(1);
    }
}

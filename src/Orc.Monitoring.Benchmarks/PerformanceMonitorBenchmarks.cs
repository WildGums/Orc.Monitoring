#pragma warning disable CL0002
namespace Orc.Monitoring.Benchmarks;

using BenchmarkDotNet.Attributes;
using System;
using System.Threading.Tasks;
using Core.Abstractions;
using Core.CallStacks;
using Core.Configuration;
using Core.Controllers;
using Core.Extensions;
using Core.Logging;
using Core.MethodCallContexts;
using Core.Monitors;
using Core.PerformanceMonitoring;
using Core.Pooling;
using Microsoft.Extensions.Logging;
using Reporters;
using Filters;

[MemoryDiagnoser]
public class PerformanceMonitorBenchmarks
{
    private IPerformanceMonitor? _performanceMonitor;
    private IMonitoringController? _monitoringController;
    private IClassMonitorFactory? _classMonitorFactory;
    private ICallStackFactory? _callStackFactory;

    [GlobalSetup]
    public void Setup()
    {
        var loggerFactory = MonitoringLoggerFactory.Instance;
        _monitoringController = new MonitoringController(loggerFactory);
        var methodCallInfoPool = new MethodCallInfoPool(_monitoringController, loggerFactory);
        var methodCallContextFactory = new MethodCallContextFactory(_monitoringController, loggerFactory, methodCallInfoPool);

        _classMonitorFactory = new ClassMonitorFactory(_monitoringController, loggerFactory, methodCallContextFactory, methodCallInfoPool);
        _callStackFactory = new CallStackFactory(_monitoringController, loggerFactory, methodCallInfoPool);

        _performanceMonitor = new PerformanceMonitor(_monitoringController, loggerFactory,
            _callStackFactory,
            _classMonitorFactory,
            () => new ConfigurationBuilder(_monitoringController));
    }

    [Benchmark]
    public void ConfigureBenchmark()
    {
        _performanceMonitor!.Configure(config =>
        {
            config.AddReporterType<WorkflowReporter>();
            config.AddFilter<WorkflowItemFilter>();
            config.TrackAssembly(typeof(PerformanceMonitorBenchmarks).Assembly);
        });
    }

    [Benchmark]
    public void ForClassBenchmark()
    {
        var monitor = _performanceMonitor!.ForClass<PerformanceMonitorBenchmarks>();
    }

    [Benchmark]
    public void StartMethodBenchmark()
    {
        var monitor = _performanceMonitor!.ForClass<PerformanceMonitorBenchmarks>();
        using var context = monitor.Start(builder => builder.AddReporter(new WorkflowReporter()));
    }

    [Benchmark]
    public async Task StartAsyncMethodBenchmark()
    {
        var monitor = _performanceMonitor!.ForClass<PerformanceMonitorBenchmarks>();
        await using var context = monitor.AsyncStart(builder => builder.AddReporter(new WorkflowReporter()));
    }

    [Benchmark]
    public void ComplexMonitoringScenarioBenchmark()
    {
        _performanceMonitor!.Configure(config =>
        {
            config.AddReporterType<WorkflowReporter>();
            config.AddFilter<WorkflowItemFilter>();
            config.TrackAssembly(typeof(PerformanceMonitorBenchmarks).Assembly);
        });

        var monitor = _performanceMonitor.ForClass<PerformanceMonitorBenchmarks>();

        using (var context = monitor.Start(builder => builder.AddReporter(new WorkflowReporter())))
        {
            // Simulate some work
            System.Threading.Thread.Sleep(1);
        }
    }
}

#pragma warning disable CL0002
namespace Orc.Monitoring.Benchmarks;

using BenchmarkDotNet.Attributes;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Moq;
using Microsoft.Extensions.Logging;
using System.Reflection;

[MemoryDiagnoser]
public class MethodCallInfoPoolBenchmarks
{
    private MethodCallInfoPool? _methodCallInfoPool;
    private IMonitoringController? _monitoringController;
    private Mock<IClassMonitor>? _mockClassMonitor;
    private Type? _testClassType;
    private MethodInfo? _testMethodInfo;

    [GlobalSetup]
    public void Setup()
    {
        var loggerFactory = MonitoringLoggerFactory.Instance;
        _monitoringController = new MonitoringController(loggerFactory);
        _methodCallInfoPool = new MethodCallInfoPool(_monitoringController, loggerFactory);
        _mockClassMonitor = new Mock<IClassMonitor>();
        _testClassType = typeof(MethodCallInfoPoolBenchmarks);
        _testMethodInfo = _testClassType.GetMethod(nameof(Setup)) ?? throw new InvalidOperationException("Test method not found");

        _monitoringController.Enable();
    }

    [Benchmark]
    public void RentAndReturnBenchmark()
    {
        var methodCallInfo = _methodCallInfoPool!.Rent(_mockClassMonitor!.Object, _testClassType!, _testMethodInfo!, Array.Empty<Type>(), Guid.NewGuid().ToString(), new Dictionary<string, string>());
        _methodCallInfoPool.Return(methodCallInfo);
    }

    [Benchmark]
    public async Task UseAndReturnBenchmark()
    {
        var methodCallInfo = _methodCallInfoPool!.Rent(_mockClassMonitor!.Object, _testClassType!, _testMethodInfo!, Array.Empty<Type>(), Guid.NewGuid().ToString(), new Dictionary<string, string>());
        await using (var _ = _methodCallInfoPool.UseAndReturn(methodCallInfo))
        {
            // Simulating some work
            await Task.Delay(1);
        }
    }

    [Benchmark]
    public async Task ConcurrentRentReturnBenchmark()
    {
        const int concurrentOperations = 1000;
        var tasks = new Task[concurrentOperations];

        for (int i = 0; i < concurrentOperations; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                var methodCallInfo = _methodCallInfoPool!.Rent(_mockClassMonitor!.Object, _testClassType!, _testMethodInfo!, Array.Empty<Type>(), Guid.NewGuid().ToString(), new Dictionary<string, string>());
                _methodCallInfoPool.Return(methodCallInfo);
            });
        }

        await Task.WhenAll(tasks);
    }
}

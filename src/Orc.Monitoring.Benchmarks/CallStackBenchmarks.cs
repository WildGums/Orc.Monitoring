#pragma warning disable CL0002
namespace Orc.Monitoring.Benchmarks;

using BenchmarkDotNet.Attributes;
using System;
using System.Threading.Tasks;
using System.Threading;
using Moq;
using Microsoft.Extensions.Logging;

[MemoryDiagnoser]
public class CallStackBenchmarks
{
    private CallStack? _callStack;
    private Mock<IClassMonitor>? _mockClassMonitor;
    private MonitoringConfiguration? _config;
    private IMonitoringController? _monitoringController;
    private MethodCallInfoPool? _methodCallInfoPool;

    [GlobalSetup]
    public void Setup()
    {
        var loggerFactory = MonitoringLoggerFactory.Instance;
        _config = new MonitoringConfiguration();
        _monitoringController = new MonitoringController(loggerFactory);
        _methodCallInfoPool = new MethodCallInfoPool(_monitoringController, loggerFactory);

        _callStack = new CallStack(_monitoringController, _config, _methodCallInfoPool, loggerFactory);
        _mockClassMonitor = new Mock<IClassMonitor>();

        _monitoringController.Enable();
    }

    [Benchmark]
    public void PushAndPopBenchmark()
    {
        var methodInfo = CreateMethodCallInfo("TestMethod");
        _callStack!.Push(methodInfo);
        _callStack.Pop(methodInfo);
    }

    [Benchmark]
    public void CreateMethodCallInfoBenchmark()
    {
        CreateMethodCallInfo("TestMethod");
    }

    [Benchmark]
    public async Task ConcurrentPushPopBenchmark()
    {
        const int concurrentOperations = 1000;
        var tasks = new Task[concurrentOperations];

        for (int i = 0; i < concurrentOperations; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                var methodInfo = CreateMethodCallInfo($"TestMethod{i}");
                _callStack!.Push(methodInfo);
                Thread.Sleep(1); // Simulate some work
                _callStack.Pop(methodInfo);
            });
        }

        await Task.WhenAll(tasks);
    }

    private MethodCallInfo CreateMethodCallInfo(string methodName)
    {
        var config = new MethodCallContextConfig
        {
            ClassType = typeof(CallStackBenchmarks),
            CallerMethodName = methodName
        };

        return _callStack!.CreateMethodCallInfo(_mockClassMonitor!.Object, typeof(CallStackBenchmarks), config);
    }
}

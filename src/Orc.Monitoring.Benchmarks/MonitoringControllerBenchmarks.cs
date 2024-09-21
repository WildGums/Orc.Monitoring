namespace Orc.Monitoring.Benchmarks;

using BenchmarkDotNet.Attributes;
using Core.Controllers;
using Core.Models;
using Monitoring;
using Reporters;
using Filters;
using Utilities.Logging;

[MemoryDiagnoser]
public class MonitoringControllerBenchmarks
{
    private static readonly Type ReporterType = typeof(WorkflowReporter);
    private static readonly Type FilterType = typeof(WorkflowItemFilter);
    private static readonly MonitoringLoggerFactory LoggerFactory = MonitoringLoggerFactory.Instance;
    private MonitoringVersion _testVersion;
    private MonitoringController? _monitoringController;

    [GlobalSetup]
    public void Setup()
    {
        _monitoringController = new MonitoringController(LoggerFactory);
        _monitoringController.Enable();
        _monitoringController.EnableReporter(ReporterType);
        _monitoringController.EnableFilter(FilterType);
        _testVersion = _monitoringController.GetCurrentVersion();
    }

    [Benchmark]
    public bool ShouldTrackBenchmark()
    {
        return _monitoringController!.ShouldTrack(_testVersion, ReporterType, FilterType);
    }

    [Benchmark]
    public void EnableReporterBenchmark()
    {
        _monitoringController!.EnableReporter(ReporterType);
    }

    [Benchmark]
    public void DisableReporterBenchmark()
    {
        _monitoringController!.DisableReporter(ReporterType);
    }

    [Benchmark]
    public void TemporarilyEnableReporterBenchmark()
    {
        using (_monitoringController!.TemporarilyEnableReporter<WorkflowReporter>())
        {
            var currentVersion = _monitoringController!.GetCurrentVersion();
            _monitoringController!.ShouldTrack(currentVersion, ReporterType, FilterType);
        }
    }

    [Benchmark]
    public void EnableFilterBenchmark()
    {
        _monitoringController!.EnableFilter(FilterType);
    }

    [Benchmark]
    public void DisableFilterBenchmark()
    {
        _monitoringController!.DisableFilter(FilterType);
    }

    [Benchmark]
    public void GlobalEnableDisableBenchmark()
    {
        _monitoringController!.Enable();
        _monitoringController!.Disable();
    }

    [Benchmark]
    public MonitoringVersion GetCurrentVersionBenchmark()
    {
        return _monitoringController!.GetCurrentVersion();
    }

    [Benchmark]
    public bool VersionComparisonBenchmark()
    {
        var currentVersion = _monitoringController!.GetCurrentVersion();
        return currentVersion == _testVersion;
    }

    [Benchmark]
    public void BeginOperationBenchmark()
    {
        using (_monitoringController!.BeginOperation(out var operationVersion))
        {
            _ = _monitoringController!.ShouldTrack(operationVersion, ReporterType, FilterType);
        }
    }
}

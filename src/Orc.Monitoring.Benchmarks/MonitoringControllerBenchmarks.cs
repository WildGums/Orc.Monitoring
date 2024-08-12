namespace Orc.Monitoring.Benchmarks;

using BenchmarkDotNet.Attributes;
using Monitoring;
using Reporters;
using Filters;


[MemoryDiagnoser]
public class MonitoringControllerBenchmarks
{
    private static readonly Type ReporterType = typeof(WorkflowReporter);
    private static readonly Type FilterType = typeof(WorkflowItemFilter);
    private MonitoringVersion _testVersion;

    [GlobalSetup]
    public void Setup()
    {
        MonitoringController.Enable();
        MonitoringController.EnableReporter(ReporterType);
        MonitoringController.EnableFilter(FilterType);
        _testVersion = MonitoringController.GetCurrentVersion();
    }

    [Benchmark]
    public bool ShouldTrackBenchmark()
    {
        return MonitoringController.ShouldTrack(_testVersion, ReporterType, FilterType);
    }

    [Benchmark]
    public void EnableReporterBenchmark()
    {
        MonitoringController.EnableReporter(ReporterType);
    }

    [Benchmark]
    public void DisableReporterBenchmark()
    {
        MonitoringController.DisableReporter(ReporterType);
    }

    [Benchmark]
    public void TemporarilyEnableReporterBenchmark()
    {
        using (MonitoringController.TemporarilyEnableReporter<WorkflowReporter>())
        {
            var currentVersion = MonitoringController.GetCurrentVersion();
            MonitoringController.ShouldTrack(currentVersion, ReporterType, FilterType);
        }
    }

    [Benchmark]
    public void EnableFilterBenchmark()
    {
        MonitoringController.EnableFilter(FilterType);
    }

    [Benchmark]
    public void DisableFilterBenchmark()
    {
        MonitoringController.DisableFilter(FilterType);
    }

    [Benchmark]
    public void GlobalEnableDisableBenchmark()
    {
        MonitoringController.Enable();
        MonitoringController.Disable();
    }

    [Benchmark]
    public MonitoringVersion GetCurrentVersionBenchmark()
    {
        return MonitoringController.GetCurrentVersion();
    }

    [Benchmark]
    public bool VersionComparisonBenchmark()
    {
        var currentVersion = MonitoringController.GetCurrentVersion();
        return currentVersion == _testVersion;
    }

    [Benchmark]
    public void BeginOperationBenchmark()
    {
        using (MonitoringController.BeginOperation(out var operationVersion))
        {
            _ = MonitoringController.ShouldTrack(operationVersion, ReporterType, FilterType);
        }
    }
}

namespace Orc.Monitoring.Benchmarks;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Orc.Monitoring;
using Orc.Monitoring.Reporters;
using Orc.Monitoring.Filters;


[MemoryDiagnoser]
public class MonitoringManagerBenchmarks
{
    private static readonly Type ReporterType = typeof(WorkflowReporter);
    private static readonly Type FilterType = typeof(WorkflowItemFilter);

    [GlobalSetup]
    public void Setup()
    {
        MonitoringManager.Enable();
        MonitoringManager.EnableReporter(ReporterType);
        MonitoringManager.EnableFilter(FilterType);
    }

    [Benchmark]
    public bool ShouldTrackBenchmark()
    {
        return MonitoringManager.ShouldTrack(MonitoringManager.CurrentVersion, ReporterType, FilterType);
    }

    [Benchmark]
    public void EnableReporterBenchmark()
    {
        MonitoringManager.EnableReporter(ReporterType);
    }

    [Benchmark]
    public void DisableReporterBenchmark()
    {
        MonitoringManager.DisableReporter(ReporterType);
    }

    [Benchmark]
    public void TemporarilyEnableReporterBenchmark()
    {
        using (MonitoringManager.TemporarilyEnableReporter(ReporterType))
        {
            MonitoringManager.ShouldTrack(MonitoringManager.CurrentVersion, ReporterType, FilterType);
        }
    }

    [Benchmark]
    public void EnableFilterBenchmark()
    {
        MonitoringManager.EnableFilter(FilterType);
    }

    [Benchmark]
    public void DisableFilterBenchmark()
    {
        MonitoringManager.DisableFilter(FilterType);
    }
}

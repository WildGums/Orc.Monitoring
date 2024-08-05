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
        MonitoringController.Enable();
        MonitoringController.EnableReporter(ReporterType);
        MonitoringController.EnableFilter(FilterType);
    }

    [Benchmark]
    public bool ShouldTrackBenchmark()
    {
        return MonitoringController.ShouldTrack(MonitoringController.CurrentVersion, ReporterType, FilterType);
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
        using (MonitoringController.TemporarilyEnableReporter(ReporterType))
        {
            MonitoringController.ShouldTrack(MonitoringController.CurrentVersion, ReporterType, FilterType);
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
}

namespace Orc.Monitoring.Benchmarks
{
    using BenchmarkDotNet.Running;

    internal class Program
    {
        public static void Main(string[] args)
        {
            var summaryMonitoringController = BenchmarkRunner.Run<MonitoringControllerBenchmarks>();
            var summaryCallStack = BenchmarkRunner.Run<CallStackBenchmarks>();
            var summaryMethodCallInfoPool = BenchmarkRunner.Run<MethodCallInfoPoolBenchmarks>();
            var summaryVersionManager = BenchmarkRunner.Run<VersionManagerBenchmarks>();
            var summaryReportOutput = BenchmarkRunner.Run<ReportOutputBenchmarks>();
            var summaryFilter = BenchmarkRunner.Run<FilterBenchmarks>();
            var summaryPerformanceMonitor = BenchmarkRunner.Run<PerformanceMonitorBenchmarks>();
            var summaryAsyncOperations = BenchmarkRunner.Run<AsyncOperationBenchmarks>();
            var summaryConcurrency = BenchmarkRunner.Run<ConcurrencyBenchmarks>();
        }
    }
}

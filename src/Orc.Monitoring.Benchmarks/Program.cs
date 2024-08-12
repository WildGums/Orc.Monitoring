namespace Orc.Monitoring.Benchmarks
{
    using BenchmarkDotNet.Running;

    internal class Program
    {
        public static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run<MonitoringControllerBenchmarks>();
        }
    }
}

namespace Orc.Monitoring.Benchmarks
{
    using System.Reflection;
    using BenchmarkDotNet.Configs;
    using BenchmarkDotNet.Running;

    internal class Program
    {
        public static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run<MonitoringManagerBenchmarks>();
        }
    }
}

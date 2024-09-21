#pragma warning disable CL0002
namespace Orc.Monitoring.Benchmarks;

using BenchmarkDotNet.Attributes;
using System;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Linq;
using Core.Controllers;
using Core.Models;

[MemoryDiagnoser]
public class VersionManagerBenchmarks
{
    private VersionManager? _versionManager;

    [GlobalSetup]
    public void Setup()
    {
        _versionManager = new VersionManager();
    }

    [Benchmark]
    public MonitoringVersion GetNextVersionBenchmark()
    {
        return _versionManager!.GetNextVersion();
    }

    [Benchmark]
    public void VersionComparisonBenchmark()
    {
        var version1 = _versionManager!.GetNextVersion();
        var version2 = _versionManager.GetNextVersion();
        _ = version1 < version2;
        _ = version1 == version2;
        _ = version1 > version2;
    }

    [Benchmark]
    public async Task ConcurrentGetNextVersionBenchmark()
    {
        const int concurrentOperations = 1000;
        var versions = new ConcurrentBag<MonitoringVersion>();

        var tasks = Enumerable.Range(0, concurrentOperations).Select(_ => Task.Run(() =>
        {
            versions.Add(_versionManager!.GetNextVersion());
        })).ToArray();

        await Task.WhenAll(tasks);

        // Ensure all versions are unique
        var uniqueVersions = new HashSet<MonitoringVersion>(versions);
        if (uniqueVersions.Count != concurrentOperations)
        {
            throw new InvalidOperationException("Not all versions are unique");
        }
    }

    [Benchmark]
    public void RapidSuccessiveVersionsBenchmark()
    {
        const int versionCount = 10000;
        var versions = new MonitoringVersion[versionCount];

        for (int i = 0; i < versionCount; i++)
        {
            versions[i] = _versionManager!.GetNextVersion();
        }

        // Ensure all versions are in ascending order
        for (int i = 1; i < versionCount; i++)
        {
            if (versions[i] <= versions[i - 1])
            {
                throw new InvalidOperationException("Versions are not in ascending order");
            }
        }
    }
}

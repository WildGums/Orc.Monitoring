#pragma warning disable CL0002
namespace Orc.Monitoring.Benchmarks;

using BenchmarkDotNet.Attributes;
using System;
using System.IO;
using System.Threading.Tasks;
using Moq;
using Microsoft.Extensions.Logging;
using Reporters;
using Reporters.ReportOutputs;
using MethodLifeCycleItems;
using Tests;
using TestUtilities.Mocks;
using TestUtilities.TestHelpers;

[MemoryDiagnoser]
public class ReportOutputBenchmarks
{
    private CsvReportOutput? _csvReportOutput;
    private RanttOutput? _ranttOutput;
    private TxtReportOutput? _txtReportOutput;
    private Mock<IMethodCallReporter>? _mockReporter;
    private MethodCallInfo? _testMethodCallInfo;
    private string? _testOutputPath;
    private IMonitoringController? _monitoringController;
    private MethodCallInfoPool? _methodCallInfoPool;
#pragma warning disable IDISP008
#pragma warning disable IDISP006
    private InMemoryFileSystem? _fileSystem;
#pragma warning restore IDISP006
#pragma warning restore IDISP008
    private ReportOutputHelper? _reportOutputHelper;

    [GlobalSetup]
    public void Setup()
    {
        var loggerFactory = MonitoringLoggerFactory.Instance;
        _monitoringController = new MonitoringController(loggerFactory);
        _methodCallInfoPool = new MethodCallInfoPool(_monitoringController, loggerFactory);
#pragma warning disable IDISP003
        _fileSystem = new InMemoryFileSystem(loggerFactory);
#pragma warning restore IDISP003
        var csvUtils = new CsvUtils(_fileSystem);
        var reportArchiver = new ReportArchiver(_fileSystem, loggerFactory);

        _testOutputPath = _fileSystem.Combine(_fileSystem.GetTempPath(), _fileSystem.GetRandomFileName());
        _fileSystem.CreateDirectory(_testOutputPath);

        _reportOutputHelper = new ReportOutputHelper(loggerFactory, new ReportItemFactory(loggerFactory));

        _csvReportOutput = new CsvReportOutput(loggerFactory, _reportOutputHelper,
            (outputDirectory) => new MethodOverrideManager(outputDirectory, loggerFactory, _fileSystem, csvUtils),
            _fileSystem, reportArchiver);
        _csvReportOutput.SetParameters(CsvReportOutput.CreateParameters(_testOutputPath, "CsvTest"));

        _ranttOutput = new RanttOutput(loggerFactory,
            () => new EnhancedDataPostProcessor(loggerFactory),
            _reportOutputHelper,
            (outputDirectory) => new MethodOverrideManager(outputDirectory, loggerFactory, _fileSystem, csvUtils),
            _fileSystem, reportArchiver, new ReportItemFactory(loggerFactory));
        _ranttOutput.SetParameters(RanttOutput.CreateParameters(_testOutputPath));

        _txtReportOutput = new TxtReportOutput(loggerFactory, _reportOutputHelper, reportArchiver, _fileSystem);
        _txtReportOutput.SetParameters(TxtReportOutput.CreateParameters(_testOutputPath, "TxtTest"));

        _mockReporter = new Mock<IMethodCallReporter>();
        _mockReporter.Setup(r => r.Name).Returns("TestReporter");
        _mockReporter.Setup(r => r.FullName).Returns("TestReporter");

        var testMethodInfo = new TestMethodInfo("TestMethod", typeof(ReportOutputBenchmarks));
        _testMethodCallInfo = _methodCallInfoPool.Rent(null, typeof(ReportOutputBenchmarks), testMethodInfo,
            Array.Empty<Type>(), Guid.NewGuid().ToString(), new Dictionary<string, string>());

        _monitoringController.Enable();
    }


    [Benchmark]
    public async Task CsvReportOutputWriteItemBenchmark()
    {
        await using var _ = _csvReportOutput!.Initialize(_mockReporter!.Object);
        var methodCallStart = new MethodCallStart(_testMethodCallInfo!);
        _csvReportOutput.WriteItem(methodCallStart);
    }

    [Benchmark]
    public async Task RanttOutputWriteItemBenchmark()
    {
        await using var _ = _ranttOutput!.Initialize(_mockReporter!.Object);
        var methodCallStart = new MethodCallStart(_testMethodCallInfo!);
        _ranttOutput.WriteItem(methodCallStart);
    }

    [Benchmark]
    public async Task TxtReportOutputWriteItemBenchmark()
    {
        await using var _ = _txtReportOutput!.Initialize(_mockReporter!.Object);
        var methodCallStart = new MethodCallStart(_testMethodCallInfo!);
        _txtReportOutput.WriteItem(methodCallStart);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _fileSystem!.Dispose();
        _fileSystem!.DeleteDirectory(_testOutputPath!, true);
    }
}

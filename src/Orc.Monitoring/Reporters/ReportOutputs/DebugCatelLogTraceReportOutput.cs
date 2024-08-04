namespace Orc.Monitoring.Reporters.ReportOutputs;

using System;
using System.Reactive.Disposables;
using MethodLifeCycleItems;
using Microsoft.Extensions.Logging;
using Orc.Monitoring.Reporters;

public sealed class DebugCatelLogTraceReportOutput : IReportOutput
{
    private readonly ILogger<DebugCatelLogTraceReportOutput> _logger = MonitoringManager.CreateLogger<DebugCatelLogTraceReportOutput>();
    private readonly ReportOutputHelper _helper = new();

    private string? _prefix;

    public IAsyncDisposable Initialize(IMethodCallReporter reporter)
    {
        _helper.Initialize(reporter);

        return new AsyncDisposable(async () => { });
    }

    public void SetParameters(object? parameter)
    {
        // Ignored
    }

    public void WriteSummary(string message)
    {
        _logger.LogDebug($"{GetPrefix()}{message}");
    }

    public void WriteItem(ICallStackItem callStackItem, string? message = null)
    {
        _helper.ProcessCallStackItem(callStackItem);
        var messageToLog = message ?? callStackItem.ToString();
        _logger.LogDebug($"{GetPrefix()}{messageToLog}");
    }

    public void WriteError(Exception exception)
    {
        _logger.LogDebug($"{GetPrefix()}{exception.Message}");
    }

    private string GetPrefix()
    {
        var reporterName = _helper.Reporter?.Name ?? string.Empty;
        var rootMethodName = _helper.Reporter?.RootMethod?.Name ?? string.Empty;

        return _prefix ??= $"[{reporterName}] {rootMethodName} ";
    }
}

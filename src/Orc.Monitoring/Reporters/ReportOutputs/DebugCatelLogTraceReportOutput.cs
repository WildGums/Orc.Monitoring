namespace Orc.Monitoring.Reporters.ReportOutputs;

using System;
using System.Reactive.Disposables;
using Catel.Logging;
using MethodLifeCycleItems;
using Orc.Monitoring.Reporters;

public sealed class DebugCatelLogTraceReportOutput : IReportOutput
{
    private static readonly ILog Log = LogManager.GetCurrentClassLogger();
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
        Log.Debug($"{GetPrefix()}{message}");
    }

    public void WriteItem(ICallStackItem callStackItem, string? message = null)
    {
        _helper.ProcessCallStackItem(callStackItem);
        var messageToLog = message ?? callStackItem.ToString();
        Log.Debug($"{GetPrefix()}{messageToLog}");
    }

    public void WriteError(Exception exception)
    {
        Log.Error($"{GetPrefix()}{exception.Message}");
    }

    private string GetPrefix()
    {
        var reporterName = _helper.Reporter?.Name ?? string.Empty;
        var rootMethodName = _helper.Reporter?.RootMethod?.Name ?? string.Empty;

        return _prefix ??= $"[{reporterName}] {rootMethodName} ";
    }
}

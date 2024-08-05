namespace Orc.Monitoring.Reporters.ReportOutputs;

using System;
using System.Collections.Generic;
using System.Linq;
using MethodLifeCycleItems;
using Microsoft.Extensions.Logging;
using Monitoring;
using Reporters;

public sealed class CatelLogReportOutput : IReportOutput
{
    private readonly ILogger<CatelLogReportOutput> _logger = MonitoringController.CreateLogger<CatelLogReportOutput>();

    private readonly ReportOutputHelper _helper = new();
    private readonly Dictionary<string, string> _prefixByWorkflowItemName = new();
    private readonly List<int> _threads = new();

    private string? _displayNameParameter;
    private string? _rootDisplayName;

    public static CatelLogReportParameter CreateParameter(string displayNameParameter)
    {
        return new CatelLogReportParameter
        {
            DisplayNameParameter = displayNameParameter
        };
    }

    public IAsyncDisposable Initialize(IMethodCallReporter reporter)
    {
        _helper.Initialize(reporter);
        return new AsyncDisposable(async () => { });
    }

    public void SetParameters(object? parameter = null)
    {
        if (parameter is null)
        {
            return;
        }

        var catelParameter = (CatelLogReportParameter)parameter;
        _displayNameParameter = catelParameter.DisplayNameParameter;
    }

    public void WriteSummary(string message)
    {
        var prefix = GetPrefix();
        _logger.LogInformation($"{prefix} {message}");
    }

    public void WriteItem(ICallStackItem callStackItem, string? message = null)
    {
        _helper.ProcessCallStackItem(callStackItem);

        var prefix = GetPrefix(callStackItem as IMethodLifeCycleItem);
        var messageToLog = message ?? callStackItem.ToString();

        _logger.LogInformation($"{prefix} {messageToLog}");
    }

    public void WriteError(Exception exception)
    {
        var prefix = GetPrefix();
        _logger.LogError($"{prefix} {exception.Message}");
    }

    private string GetPrefix(IMethodLifeCycleItem? methodLifeCycleItem = null)
    {
        var workflowItemName = GetWorkflowItemName();

        if (!_prefixByWorkflowItemName.TryGetValue(workflowItemName, out var prefix))
        {
            prefix = CreatePrefix(workflowItemName);
            _prefixByWorkflowItemName.Add(workflowItemName, prefix);
        }

        var thread = methodLifeCycleItem?.ThreadId ?? 0;
        if (!_threads.Contains(thread))
        {
            _threads.Add(thread);
            var threadIndex = _threads.IndexOf(thread);
            var threadPrefix = GetThreadPrefix(threadIndex);

            _logger.LogInformation($"{prefix} {threadPrefix} Thread{threadIndex}");
        }
        else
        {
            var threadIndex = _threads.IndexOf(thread);
            var threadPrefix = GetThreadPrefix(threadIndex);

            prefix = $"{prefix} {threadPrefix}|-";
        }

        var nestingLevel = methodLifeCycleItem?.MethodCallInfo?.Level ?? 0;
        var nestingIndentation = string.Concat(Enumerable.Repeat("--", nestingLevel));

        prefix = $"{prefix}{nestingIndentation}";

        return prefix;
    }

    private string GetWorkflowItemName()
    {
        if (_rootDisplayName is not null)
        {
            return _rootDisplayName;
        }

        var rootMethod = _helper.Reporter?.RootMethod;
        if (rootMethod is null)
        {
            _rootDisplayName = string.Empty;
            return _rootDisplayName;
        }

        _rootDisplayName = rootMethod.GetCustomAttributes(typeof(MethodCallParameterAttribute), false)
            .OfType<MethodCallParameterAttribute>()
            .FirstOrDefault(x => string.Equals(x.Name, _displayNameParameter, StringComparison.Ordinal))
            ?.Value ?? string.Empty;

        return _rootDisplayName;
    }

    private string CreatePrefix(string? workflowItemName)
    {
        var reporterName = _helper.Reporter?.Name ?? string.Empty;
        return workflowItemName is null
            ? $"[{reporterName}] "
            : $"[{reporterName}] {workflowItemName}: ";
    }

    private static string GetThreadPrefix(int threadIndex)
    {
        var indentationItem = "|   ";
        var threadPrefix = string.Concat(Enumerable.Repeat(indentationItem, threadIndex));
        return threadPrefix;
    }
}

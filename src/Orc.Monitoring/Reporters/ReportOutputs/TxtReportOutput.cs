namespace Orc.Monitoring.Reporters.ReportOutputs;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orc.Monitoring.MethodLifeCycleItems;
using Orc.Monitoring.Reporters;

public sealed class TxtReportOutput : IReportOutput, ILimitedOutput
{
    private readonly ILogger<TxtReportOutput> _logger = MonitoringController.CreateLogger<TxtReportOutput>();
    private readonly ReportOutputHelper _helper = new();
    private readonly Queue<LogEntry> _logEntries = new();
    private readonly List<int> _nestingLevels = [];

    private string? _fileName;
    private string? _folderPath;
    private string? _displayNameParameter;
    private OutputLimitOptions _limitOptions = OutputLimitOptions.Unlimited;

    public static TxtReportParameters CreateParameters(string folderPath, string displayNameParameter, OutputLimitOptions? limitOptions = null)
    {
        return new TxtReportParameters(folderPath, displayNameParameter, limitOptions);
    }

    public IAsyncDisposable Initialize(IMethodCallReporter reporter)
    {
        _helper.Initialize(reporter);

        return new AsyncDisposable(async () =>
        {
            if (_folderPath is null)
            {
                throw new InvalidOperationException("Folder path is not set");
            }

            if (!Directory.Exists(_folderPath))
            {
                Directory.CreateDirectory(_folderPath);
            }

            var rootDisplayName = GetRootDisplayName();
            rootDisplayName = rootDisplayName.Replace(" ", "_");

            _fileName = Path.Combine(_folderPath, $"{reporter.Name}_{rootDisplayName}.txt");

            await WriteLogEntriesToFileAsync();

            ReportArchiver.CreateTimestampedFileCopy(_fileName);
            _logEntries.Clear();
        });
    }

    public void SetParameters(object? parameters)
    {
        if (parameters is null)
        {
            return;
        }

        var txtParameters = (TxtReportParameters)parameters;
        _folderPath = txtParameters.FolderPath;
        _displayNameParameter = txtParameters.DisplayNameParameter;
        SetLimitOptions(txtParameters.LimitOptions);
    }

    public void WriteSummary(string message)
    {
        AddLogEntry(new LogEntry(DateTime.Now, "Summary", message));
    }

    public void WriteItem(ICallStackItem callStackItem, string? message = null)
    {
        _helper.ProcessCallStackItem(callStackItem);

        switch (callStackItem)
        {
            case IMethodLifeCycleItem methodLifeCycleItem:
                ProcessMethodLifeCycleItem(callStackItem, message, methodLifeCycleItem);
                break;

            case CallGap gap:
                ProcessGap(gap);
                break;

            default:
                AddLogEntry(new LogEntry(DateTime.Now, callStackItem.GetType().Name, message ?? callStackItem.ToString() ?? string.Empty));
                break;
        }
    }

    private void ProcessGap(CallGap gap)
    {
        var endTimestamp = gap.TimeStamp + gap.Elapsed;
        AddLogEntry(new LogEntry(gap.TimeStamp, "Gap", $"Duration: {gap.Elapsed.TotalMilliseconds:F2} ms"));

        // Add gap parameters
        if (gap.Parameters.Count > 0)
        {
            foreach (var parameter in gap.Parameters)
            {
                AddLogEntry(new LogEntry(gap.TimeStamp, "GapParameter", $"{parameter.Key}: {parameter.Value}"));
            }
        }
    }

    private void ProcessMethodLifeCycleItem(ICallStackItem callStackItem, string? message,
        IMethodLifeCycleItem methodLifeCycleItem)
    {
        var timestamp = methodLifeCycleItem.TimeStamp;
        var nestingLevel = methodLifeCycleItem.MethodCallInfo.Level;
        var nestingIndex = _nestingLevels.IndexOf(nestingLevel);
        if (nestingIndex == -1)
        {
            _nestingLevels.Add(nestingLevel);
            nestingIndex = _nestingLevels.Count - 1;
        }
        else
        {
            _nestingLevels.RemoveRange(nestingIndex + 1, _nestingLevels.Count - nestingIndex - 1);
        }

        var indentation = string.Join(string.Empty, Enumerable.Repeat("  ", nestingIndex));
        AddLogEntry(new LogEntry(timestamp, callStackItem.GetType().Name, $"{indentation}{message ?? callStackItem.ToString()}"));

        if (methodLifeCycleItem is MethodCallEnd endItem)
        {
            ProcessMethodCallEnd(endItem, timestamp, indentation);
        }
    }

    private void ProcessMethodCallEnd(MethodCallEnd endItem, DateTime timestamp, string indentation)
    {
        // Add parameters
        var parameters = endItem.MethodCallInfo.Parameters ?? [];
        if (parameters.Count > 0)
        {
            foreach (var parameter in parameters)
            {
                AddLogEntry(new LogEntry(timestamp, "Parameter", $"{indentation}  {parameter.Key}: {parameter.Value}"));
            }
        }

        // Add method duration for MethodCallEnd
        AddLogEntry(new LogEntry(timestamp, "Duration", $"{indentation}  Duration: {endItem.MethodCallInfo.Elapsed.TotalMilliseconds:F2} ms"));
    }

    public void WriteError(Exception exception)
    {
        var timestamp = DateTime.Now;
        AddLogEntry(new LogEntry(timestamp, "Error", $"Message: {exception.Message}"));
        AddLogEntry(new LogEntry(timestamp, "StackTrace", $"Stack Trace: {exception.StackTrace}"));
    }

    private string GetRootDisplayName()
    {
        var rootMethod = _helper.Reporter?.RootMethod;
        if (rootMethod is null)
        {
            return string.Empty;
        }

        return rootMethod.GetCustomAttributes(typeof(MethodCallParameterAttribute), false)
            .OfType<MethodCallParameterAttribute>()
            .FirstOrDefault(x => string.Equals(x.Name, _displayNameParameter, StringComparison.Ordinal))
            ?.Value ?? string.Empty;
    }

    private void AddLogEntry(LogEntry entry)
    {
        _logEntries.Enqueue(entry);
        ApplyLimits();
    }

    private async Task WriteLogEntriesToFileAsync()
    {
        if (_fileName is null)
        {
            throw new InvalidOperationException("File name is not set");
        }

        try
        {
            using var writer = new StreamWriter(_fileName, false, Encoding.UTF8);
            foreach (var entry in _logEntries)
            {
                await writer.WriteLineAsync($"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{entry.Category}] {entry.Message}");
            }

            _logger.LogInformation($"TXT report written to {_fileName} with {_logEntries.Count} entries");
            if (_limitOptions.MaxItems.HasValue)
            {
                _logger.LogInformation($"Output limited to {_limitOptions.MaxItems.Value} items");
            }
            if (_limitOptions.MaxAge.HasValue)
            {
                _logger.LogInformation($"Output limited to entries newer than {_limitOptions.MaxAge.Value}");
            }
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, $"Error writing to TXT file: {ex.Message}");
        }
    }

    public void SetLimitOptions(OutputLimitOptions options)
    {
        _limitOptions = options;
        ApplyLimits();
    }

    public OutputLimitOptions GetLimitOptions()
    {
        return _limitOptions;
    }

    private void ApplyLimits()
    {
        if (_limitOptions.MaxItems.HasValue)
        {
            while (_logEntries.Count > _limitOptions.MaxItems.Value)
            {
                _logEntries.Dequeue();
            }
        }

        if (_limitOptions.MaxAge.HasValue)
        {
            var cutoffTime = DateTime.Now - _limitOptions.MaxAge.Value;
            while (_logEntries.TryPeek(out var oldestEntry) && oldestEntry.Timestamp < cutoffTime)
            {
                _logEntries.Dequeue();
            }
        }
    }

    private class LogEntry
    {
        public DateTime Timestamp { get; }
        public string Category { get; }
        public string Message { get; }

        public LogEntry(DateTime timestamp, string category, string message)
        {
            Timestamp = timestamp;
            Category = category;
            Message = message;
        }
    }
}

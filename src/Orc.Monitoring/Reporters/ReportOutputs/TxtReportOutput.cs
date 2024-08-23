namespace Orc.Monitoring.Reporters.ReportOutputs;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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
        _logger.LogInformation($"Initializing {nameof(TxtReportOutput)}");
        _helper.Initialize(reporter);

        return new AsyncDisposable(async () =>
        {
            _logger.LogInformation($"Disposing {nameof(TxtReportOutput)}");
            try
            {
                if (_folderPath is null)
                {
                    throw new InvalidOperationException("Folder path is not set");
                }

                Directory.CreateDirectory(_folderPath);
                _logger.LogInformation($"Created output directory: {_folderPath}");

                var rootDisplayName = GetRootDisplayName();
                rootDisplayName = rootDisplayName.Replace(" ", "_");

                _fileName = Path.Combine(_folderPath, $"{reporter.Name}_{rootDisplayName}.txt");

                await WriteLogEntriesToFileAsync();

                var fileCreated = await WaitForFileCreationAsync(_fileName, 60); // Increase timeout to 60 seconds
                if (!fileCreated)
                {
                    _logger.LogError($"Failed to create file: {_fileName}");
                }
                else
                {
                    _logger.LogInformation($"File created successfully: {_fileName}");
                    if (File.Exists(_fileName))
                    {
                        ReportArchiver.CreateTimestampedFileCopy(_fileName);
                        _logger.LogInformation($"Created timestamped copy of {_fileName}");
                    }
                }

                _logEntries.Clear();

                _logger.LogInformation("TxtReportOutput initialization completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during TxtReportOutput initialization");
                throw;
            }
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

        _logger.LogInformation($"Parameters set: FolderPath = {_folderPath}, DisplayNameParameter = {_displayNameParameter}");
    }

    public void WriteSummary(string message)
    {
        AddLogEntry(new LogEntry(DateTime.Now, "Summary", message));
    }

    public void WriteItem(ICallStackItem callStackItem, string? message = null)
    {
        var reportItem = _helper.ProcessCallStackItem(callStackItem);
        if (reportItem is not null)
        {
            switch (callStackItem)
            {
                case IMethodLifeCycleItem methodLifeCycleItem:
                    ProcessMethodLifeCycleItem(callStackItem, message, methodLifeCycleItem);
                    break;

                case CallGap gap:
                    ProcessGap(gap);
                    break;

                default:
                    AddLogEntry(new LogEntry(DateTime.Now, callStackItem.GetType().Name, $"{reportItem.MethodName}: {message ?? callStackItem.ToString() ?? string.Empty}"));
                    break;
            }
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
        _logger.LogDebug($"Added log entry: {entry.Category} - {entry.Message}");
    }

    private async Task WriteLogEntriesToFileAsync()
    {
        if (_fileName is null)
        {
            throw new InvalidOperationException("File name is not set");
        }

        try
        {
            _logger.LogInformation($"Starting to write log entries to file: {_fileName}");
            _logger.LogInformation($"Number of log entries: {_logEntries.Count}");

            var limitedEntries = _logEntries
                .OrderByDescending(e => e.Timestamp)
                .Take(_limitOptions.MaxItems ?? int.MaxValue)
                .Where(e => _limitOptions.MaxAge is null || e.Timestamp >= DateTime.Now - _limitOptions.MaxAge.Value)
                .OrderBy(e => e.Timestamp);

            await using (var writer = new StreamWriter(_fileName, false, Encoding.UTF8))
            {
                foreach (var entry in limitedEntries)
                {
                    await writer.WriteLineAsync($"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{entry.Category}] {entry.Message}");
                }
            }

            _logger.LogInformation($"TXT report written to {_fileName} with {limitedEntries.Count()} entries");

            if (File.Exists(_fileName))
            {
                var fileContent = await File.ReadAllTextAsync(_fileName);
                var lineCount = fileContent.Split('\n').Length;
                _logger.LogInformation($"Actual line count in file: {lineCount}");
            }
            else
            {
                _logger.LogWarning($"File not found after writing: {_fileName}");
            }
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, $"Error writing to TXT file: {ex.Message}");
            throw;
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
        int removedItems = 0;

        if (_limitOptions.MaxItems.HasValue)
        {
            while (_logEntries.Count > _limitOptions.MaxItems.Value)
            {
                _logEntries.Dequeue();
                removedItems++;
            }
        }

        if (_limitOptions.MaxAge.HasValue)
        {
            var cutoffTime = DateTime.Now - _limitOptions.MaxAge.Value;
            while (_logEntries.TryPeek(out var oldestEntry) && oldestEntry.Timestamp < cutoffTime)
            {
                _logEntries.Dequeue();
                removedItems++;
            }
        }

        _logger.LogInformation($"Applied limits. Removed items: {removedItems}");
        _logger.LogInformation($"Current log entries count: {_logEntries.Count}");
    }

    public string GetDebugInfo() => _helper.GetDebugInfo();

    private async Task<bool> WaitForFileCreationAsync(string filePath, int timeoutSeconds)
    {
        for (int i = 0; i < timeoutSeconds; i++)
        {
            if (File.Exists(filePath))
            {
                _logger.LogInformation($"File created after {i} seconds: {filePath}");
                return true;
            }
            await Task.Delay(1000);
        }
        _logger.LogWarning($"File not created after {timeoutSeconds} seconds: {filePath}");
        return false;
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

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

    public TxtReportOutput()
    {
        _logger.LogDebug("Creating TxtReportOutput instance");
    }

    public static TxtReportParameters CreateParameters(string folderPath, string displayNameParameter, OutputLimitOptions? limitOptions = null)
    {
        ArgumentNullException.ThrowIfNull(folderPath);
        ArgumentNullException.ThrowIfNull(displayNameParameter);

        return new TxtReportParameters(folderPath, displayNameParameter, limitOptions);
    }

    public IAsyncDisposable Initialize(IMethodCallReporter reporter)
    {
        _logger.LogInformation($"Initializing {nameof(TxtReportOutput)}");
        _helper.Initialize(reporter);

        if (_folderPath is null)
        {
            throw new InvalidOperationException("Folder path is not set");
        }

        Directory.CreateDirectory(_folderPath);
        _logger.LogInformation($"Created output directory: {_folderPath}");

        var rootDisplayName = GetRootDisplayName();
        rootDisplayName = rootDisplayName.Replace(" ", "_");

        _fileName = Path.Combine(_folderPath, $"{reporter.Name}_{rootDisplayName}.txt");
        _logger.LogInformation($"File name set to: {_fileName}");

        // Create an empty file to ensure it exists
        File.WriteAllText(_fileName, string.Empty);
        _logger.LogInformation($"Empty file created: {_fileName}");

        return new AsyncDisposable(async () =>
        {
            _logger.LogInformation($"Disposing {nameof(TxtReportOutput)}");
            try
            {
                await WriteLogEntriesToFileAsync();
                _logger.LogInformation($"Log entries written to file: {_fileName}");

                if (File.Exists(_fileName))
                {
                    ReportArchiver.CreateTimestampedFileCopy(_fileName);
                    _logger.LogInformation($"Created timestamped copy of {_fileName}");
                }
                else
                {
                    _logger.LogWarning($"File not found during dispose: {_fileName}");
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
            _logger.LogWarning("Root method is null when getting root display name");
            return "DefaultDisplay";
        }

        var attribute = rootMethod.GetCustomAttributes(typeof(MethodCallParameterAttribute), false)
            .OfType<MethodCallParameterAttribute>()
            .FirstOrDefault(x => string.Equals(x.Name, _displayNameParameter, StringComparison.Ordinal));

        if (attribute is null)
        {
            _logger.LogWarning($"No MethodCallParameterAttribute found with name '{_displayNameParameter}'");
            return "DefaultDisplay";
        }

        var value = attribute.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            _logger.LogWarning("Display name value is null or whitespace");
            return "DefaultDisplay";
        }

        _logger.LogDebug($"Root display name retrieved: {value}");
        return value;
    }

    private void AddLogEntry(LogEntry entry)
    {
        _logEntries.Enqueue(entry);
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
            _logger.LogInformation($"Number of log entries before applying limits: {_logEntries.Count}");
            _logger.LogInformation($"Current limit options: MaxItems = {_limitOptions.MaxItems}, MaxAge = {_limitOptions.MaxAge}");

            var limitedEntries = ApplyLimits(_logEntries.ToList());

            _logger.LogInformation($"Writing {limitedEntries.Count} entries to file:");
            await using (var writer = new StreamWriter(_fileName, false, Encoding.UTF8))
            {
                foreach (var entry in limitedEntries)
                {
                    var line = $"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{entry.Category}] {entry.Message}";
                    await writer.WriteLineAsync(line);
                    _logger.LogInformation($"Writing entry: {line}");
                }
            }

            _logger.LogInformation($"TXT report written to {_fileName} with {limitedEntries.Count} entries");

            if (File.Exists(_fileName))
            {
                var fileContent = await File.ReadAllTextAsync(_fileName);
                _logger.LogInformation($"File content:\n{fileContent}");
                var lineCount = fileContent.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
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
        _logger.LogInformation($"Set limit options: MaxItems = {options.MaxItems}, MaxAge = {options.MaxAge}");
        // We're not applying limits here anymore, just logging the new options
    }

    public OutputLimitOptions GetLimitOptions()
    {
        _logger.LogDebug($"Getting limit options: MaxItems = {_limitOptions.MaxItems}, MaxAge = {_limitOptions.MaxAge}");
        return _limitOptions;
    }

    private List<LogEntry> ApplyLimits(List<LogEntry> entries)
    {
        var now = DateTime.Now;
        _logger.LogInformation($"Applying limits. Current time: {now:yyyy-MM-dd HH:mm:ss.fff}");

        var limitedEntries = entries.OrderBy(e => e.Timestamp).ToList();

        _logger.LogInformation($"Original entries:");
        foreach (var entry in limitedEntries)
        {
            _logger.LogInformation($"  {entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff} - {entry.Category} - {entry.Message}");
        }

        if (_limitOptions.MaxAge.HasValue)
        {
            var cutoffTime = now - _limitOptions.MaxAge.Value;
            _logger.LogInformation($"Applying MaxAge limit. Cutoff time: {cutoffTime:yyyy-MM-dd HH:mm:ss.fff}");
            limitedEntries = limitedEntries.Where(e => e.Timestamp >= cutoffTime).ToList();
        }

        if (_limitOptions.MaxItems.HasValue)
        {
            _logger.LogInformation($"Applying MaxItems limit: {_limitOptions.MaxItems.Value}");
            limitedEntries = limitedEntries.TakeLast(_limitOptions.MaxItems.Value).ToList();
        }

        _logger.LogInformation($"Limited entries:");
        foreach (var entry in limitedEntries)
        {
            _logger.LogInformation($"  {entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff} - {entry.Category} - {entry.Message}");
        }

        _logger.LogInformation($"Applied limits. Original count: {entries.Count}, Limited count: {limitedEntries.Count}");
        _logger.LogInformation($"Oldest entry timestamp: {limitedEntries.FirstOrDefault()?.Timestamp:yyyy-MM-dd HH:mm:ss.fff}");
        _logger.LogInformation($"Newest entry timestamp: {limitedEntries.LastOrDefault()?.Timestamp:yyyy-MM-dd HH:mm:ss.fff}");

        return limitedEntries;
    }

    public string GetDebugInfo() => _helper.GetDebugInfo();

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
        var itemName = methodLifeCycleItem.MethodCallInfo.MethodName;
        AddLogEntry(new LogEntry(timestamp, callStackItem.GetType().Name, $"{indentation}{itemName}: {message ?? callStackItem.ToString()}"));

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

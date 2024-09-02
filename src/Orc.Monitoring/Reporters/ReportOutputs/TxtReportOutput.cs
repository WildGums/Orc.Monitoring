namespace Orc.Monitoring.Reporters.ReportOutputs;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IO;
using Microsoft.Extensions.Logging;
using Orc.Monitoring.MethodLifeCycleItems;
using Orc.Monitoring.Reporters;

/// <summary>
/// Provides functionality to output report data in TXT format.
/// </summary>
public sealed class TxtReportOutput : IReportOutput, ILimitableOutput
{
    private readonly ILogger<TxtReportOutput> _logger;
    private readonly ReportOutputHelper _helper;
    private readonly ReportArchiver _reportArchiver;
    private readonly IFileSystem _fileSystem;
    private readonly Queue<LogEntry> _logEntries = new();
    private readonly List<int> _nestingLevels = [];

    private string? _fileName;
    private string? _folderPath;
    private string? _displayNameParameter;
    private OutputLimitOptions _limitOptions = OutputLimitOptions.Unlimited;

    public TxtReportOutput()
    : this(MonitoringLoggerFactory.Instance, new ReportOutputHelper(MonitoringLoggerFactory.Instance), new ReportArchiver(FileSystem.Instance), FileSystem.Instance)
    {
        
    }

    public TxtReportOutput(IMonitoringLoggerFactory loggerFactory, ReportOutputHelper reportOutputHelper, ReportArchiver reportArchiver, IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(reportOutputHelper);
        ArgumentNullException.ThrowIfNull(reportArchiver);
        ArgumentNullException.ThrowIfNull(fileSystem);

        _logger = loggerFactory.CreateLogger<TxtReportOutput>();
        _helper = reportOutputHelper;
        _reportArchiver = reportArchiver;
        _fileSystem = fileSystem;

        _logger.LogDebug("Creating TxtReportOutput instance");
    }

    /// <summary>
    /// Creates parameters for TXT report output.
    /// </summary>
    /// <param name="folderPath">The folder path where the TXT file will be saved.</param>
    /// <param name="displayNameParameter">The parameter used for display name.</param>
    /// <param name="limitOptions">The output limit options for the TXT report.</param>
    /// <returns>A TxtReportParameters object.</returns>
    public static TxtReportParameters CreateParameters(string folderPath, string displayNameParameter, OutputLimitOptions? limitOptions = null)
    {
        ArgumentNullException.ThrowIfNull(folderPath);
        ArgumentNullException.ThrowIfNull(displayNameParameter);

        return new TxtReportParameters(folderPath, displayNameParameter, limitOptions);
    }

    /// <summary>
    /// Initializes the TXT report output.
    /// </summary>
    /// <param name="reporter">The method call reporter to be used.</param>
    /// <returns>An IAsyncDisposable that can be used to finalize the report.</returns>
    public IAsyncDisposable Initialize(IMethodCallReporter reporter)
    {
        _logger.LogInformation($"Initializing {nameof(TxtReportOutput)}");
        _helper.Initialize(reporter);

        if (_folderPath is null)
        {
            throw new InvalidOperationException("Folder path is not set");
        }

        _fileSystem.CreateDirectory(_folderPath);
        _logger.LogInformation($"Created output directory: {_folderPath}");

        var rootDisplayName = GetRootDisplayName();
        rootDisplayName = rootDisplayName.Replace(" ", "_");

        _fileName = Path.Combine(_folderPath, $"{reporter.Name}_{rootDisplayName}.txt");
        _logger.LogInformation($"File name set to: {_fileName}");

        // Create an empty file to ensure it exists
        _fileSystem.WriteAllText(_fileName, string.Empty);
        _logger.LogInformation($"Empty file created: {_fileName}");

        return new AsyncDisposable(async () =>
        {
            _logger.LogInformation($"Disposing {nameof(TxtReportOutput)}");
            try
            {
                await WriteLogEntriesToFileAsync();
                _logger.LogInformation($"Log entries written to file: {_fileName}");

                if (_fileSystem.FileExists(_fileName))
                {
                    _reportArchiver.CreateTimestampedFileCopy(_fileName);
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

    /// <summary>
    /// Sets the parameters for the TXT report output.
    /// </summary>
    /// <param name="parameters">The parameters to set.</param>
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

    /// <summary>
    /// Writes a summary message to the report.
    /// </summary>
    /// <param name="message">The summary message to write.</param>
    public void WriteSummary(string message)
    {
        AddLogEntry(new LogEntry(DateTime.Now, "Summary", message));
    }

    /// <summary>
    /// Writes a call stack item to the report.
    /// </summary>
    /// <param name="callStackItem">The call stack item to write.</param>
    /// <param name="message">An optional message associated with the item.</param>
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

    /// <summary>
    /// Writes an error to the report.
    /// </summary>
    /// <param name="exception">The exception to write.</param>
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
            return _displayNameParameter ?? "DefaultDisplay";
        }

        var attribute = rootMethod.GetCustomAttributes(typeof(MethodCallParameterAttribute), false)
            .OfType<MethodCallParameterAttribute>()
            .FirstOrDefault(x => string.Equals(x.Name, _displayNameParameter, StringComparison.Ordinal));

        if (attribute is null)
        {
            _logger.LogWarning($"No MethodCallParameterAttribute found with name '{_displayNameParameter}'");
            return _displayNameParameter ?? "DefaultDisplay";
        }

        var value = attribute.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            _logger.LogWarning("Display name value is null or whitespace");
            return _displayNameParameter ?? "DefaultDisplay";
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
            _logger.LogInformation($"Number of log entries: {_logEntries.Count}");
            _logger.LogInformation($"Current limit options: MaxItems = {_limitOptions.MaxItems}");

            var limitedEntries = ApplyLimits(_logEntries.ToList());

            _logger.LogInformation($"Writing {limitedEntries.Count} entries to file:");
            await using (var writer = _fileSystem.CreateStreamWriter(_fileName, false, Encoding.UTF8))
            {
                foreach (var entry in limitedEntries)
                {
                    var line = $"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{entry.Category}] {entry.Message}";
                    await writer.WriteLineAsync(line);
                    _logger.LogInformation($"Writing entry: {line}");
                }
            }

            _logger.LogInformation($"TXT report written to {_fileName} with {limitedEntries.Count} entries");

            if (_fileSystem.FileExists(_fileName))
            {
                var fileContent = await _fileSystem.ReadAllTextAsync(_fileName);
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

    /// <summary>
    /// Sets the limit options for the TXT report output.
    /// </summary>
    /// <param name="options">The output limit options to set.</param>
    public void SetLimitOptions(OutputLimitOptions options)
    {
        _limitOptions = options;
        _helper.SetLimitOptions(options);
        _logger.LogInformation($"Limit options set: MaxItems = {options.MaxItems}");
    }

    /// <summary>
    /// Gets the current limit options for the TXT report output.
    /// </summary>
    /// <returns>The current output limit options.</returns>
    public OutputLimitOptions GetLimitOptions()
    {
        return _limitOptions;
    }

    private List<LogEntry> ApplyLimits(List<LogEntry> entries)
    {
        if (_limitOptions.MaxItems.HasValue)
        {
            return entries.TakeLast(_limitOptions.MaxItems.Value).ToList();
        }
        return entries;
    }

    /// <summary>
    /// Gets debug information about the current state of the TXT report output.
    /// </summary>
    /// <returns>A string containing debug information.</returns>
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

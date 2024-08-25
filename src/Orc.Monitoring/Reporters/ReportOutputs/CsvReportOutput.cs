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

public sealed class CsvReportOutput : IReportOutput, ILimitedOutput
{
    private readonly ILogger<CsvReportOutput> _logger = MonitoringController.CreateLogger<CsvReportOutput>();
    private readonly ReportOutputHelper _helper = new();
    private List<ReportItem> _reportItems = new();

    private string? _fileName;
    private string? _folderPath;
    private MethodOverrideManager? _methodOverrideManager;
    private OutputLimitOptions _limitOptions = OutputLimitOptions.Unlimited;

    public static CsvReportParameters CreateParameters(string folderPath, string fileName, OutputLimitOptions? limitOptions = null)
    {
        ArgumentNullException.ThrowIfNull(folderPath);
        ArgumentNullException.ThrowIfNull(fileName);

        return new()
        {
            FolderPath = folderPath,
            FileName = fileName,
            LimitOptions = limitOptions ?? OutputLimitOptions.Unlimited
        };
    }

    public IAsyncDisposable Initialize(IMethodCallReporter reporter)
    {
        _logger.LogInformation($"Initializing {nameof(CsvReportOutput)}");
        _helper.Initialize(reporter);

        return new AsyncDisposable(async () =>
        {
            _logger.LogInformation($"Disposing {nameof(CsvReportOutput)}");
            try
            {
                await ExportToCsvAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error during {nameof(CsvReportOutput)} export");
                throw;
            }
        });
    }

    public void SetParameters(object? parameter = null)
    {
        if (parameter is null)
        {
            return;
        }

        var parameters = (CsvReportParameters)parameter;
        _folderPath = parameters.FolderPath;
        _fileName = parameters.FileName;
        _limitOptions = parameters.LimitOptions;

        _methodOverrideManager = new MethodOverrideManager(_folderPath);
        _methodOverrideManager.LoadOverrides();

        _logger.LogInformation($"Parameters set: FolderPath = {_folderPath}, FileName = {_fileName}");
    }

    public void WriteSummary(string message)
    {
        // Ignored in CSV output
    }

    public void WriteItem(ICallStackItem callStackItem, string? message = null)
    {
        var reportItem = _helper.ProcessCallStackItem(callStackItem);
        if (reportItem is not null)
        {
            if (callStackItem is MethodCallStart methodCallStart)
            {
                reportItem.ItemName = methodCallStart.MethodCallInfo.MethodName;
                reportItem.MethodName = methodCallStart.MethodCallInfo.MethodName;
            }
            AddReportItem(reportItem);
        }
    }

    public void WriteError(Exception exception)
    {
        _logger.LogError(exception, "Error occurred during CSV report generation");
    }

    private void AddReportItem(ReportItem item)
    {
        _reportItems.Add(item);
        _logger.LogDebug($"Added report item: {item.ItemName ?? item.MethodName}, StartTime: {item.StartTime}. Current count: {_reportItems.Count}");
        ApplyLimits();
    }

    private void ApplyLimits()
    {
        if (_limitOptions.MaxAge.HasValue)
        {
            var cutoffTime = DateTime.Now - _limitOptions.MaxAge.Value;
            var removedCount = _reportItems.RemoveAll(i => !string.IsNullOrEmpty(i.StartTime) && DateTime.TryParse(i.StartTime, out var startTime) && startTime < cutoffTime);
            _logger.LogDebug($"Removed {removedCount} items due to age limit");
        }

        if (_limitOptions.MaxItems.HasValue)
        {
            _reportItems = _reportItems
                .OrderByDescending(i => DateTime.Parse(i.StartTime ?? DateTime.MinValue.ToString()))
                .Take(_limitOptions.MaxItems.Value)
                .ToList();
            _logger.LogDebug($"Applied item limit. Current count: {_reportItems.Count}");
        }

        _logger.LogInformation($"After applying limits - Item count: {_reportItems.Count}");
    }

    private async Task ExportToCsvAsync()
    {
        if (_methodOverrideManager is null || _folderPath is null || _fileName is null)
        {
            throw new InvalidOperationException("Method override manager, folder path, or file name is not set");
        }

        Directory.CreateDirectory(_folderPath);

        var fullPath = Path.Combine(_folderPath, $"{_fileName}.csv");

        try
        {
            _logger.LogInformation($"Starting CSV export to {fullPath}");
            _logger.LogInformation($"Number of report items: {_reportItems.Count}");

            // Sort items by StartTime in descending order
            var sortedItems = _reportItems.OrderByDescending(item => DateTime.Parse(item.StartTime ?? DateTime.MinValue.ToString())).ToList();

            // Log details of items being exported
            for (int i = 0; i < sortedItems.Count; i++)
            {
                var item = sortedItems[i];
                _logger.LogDebug($"Exporting item {i}: {item.ItemName ?? item.MethodName}, MethodName: {item.MethodName}, StartTime: {item.StartTime}");
            }

            await using (var writer = new StreamWriter(fullPath, false, Encoding.UTF8))
            {
                var csvReportWriter = new CsvReportWriter(writer, sortedItems, _methodOverrideManager);
                await csvReportWriter.WriteReportItemsCsvAsync();
            }

            _logger.LogInformation($"CSV report written to {fullPath} with {_reportItems.Count} items");

            // Verify file content
            var fileContent = await File.ReadAllTextAsync(fullPath);
            var lineCount = fileContent.Split('\n').Length;
            _logger.LogInformation($"Actual line count in file: {lineCount}");

            if (_limitOptions.MaxItems.HasValue)
            {
                _logger.LogInformation($"Output limited to {_limitOptions.MaxItems.Value} items");
            }
            if (_limitOptions.MaxAge.HasValue)
            {
                _logger.LogInformation($"Output limited to items newer than {_limitOptions.MaxAge.Value}");
            }
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, $"Error writing to CSV file: {ex.Message}");
            throw;
        }

        try
        {
            ReportArchiver.CreateTimestampedFileCopy(fullPath);
            _logger.LogInformation($"Created timestamped copy of {fullPath}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error creating timestamped copy of {fullPath}");
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

    public string GetDebugInfo() => _helper.GetDebugInfo();

    public int GetReportItemsCount()
    {
        return _reportItems.Count;
    }
}

namespace Orc.Monitoring.Reporters.ReportOutputs;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orc.Monitoring.MethodLifeCycleItems;
using Orc.Monitoring.Reporters;

/// <summary>
/// Provides functionality to output report data in CSV format.
/// </summary>
public sealed class CsvReportOutput : IReportOutput, ILimitableOutput
{
    private readonly ILogger<CsvReportOutput> _logger = MonitoringController.CreateLogger<CsvReportOutput>();
    private readonly ReportOutputHelper _helper = new();

    private string? _fileName;
    private string? _folderPath;
    private MethodOverrideManager? _methodOverrideManager;
    private OutputLimitOptions _limitOptions = OutputLimitOptions.Unlimited;

    /// <summary>
    /// Creates parameters for CSV report output.
    /// </summary>
    /// <param name="folderPath">The folder path where the CSV file will be saved.</param>
    /// <param name="fileName">The file name for the CSV report (without extension).</param>
    /// <param name="limitOptions">The output limit options for the CSV report.</param>
    /// <returns>A CsvReportParameters object.</returns>
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

    /// <summary>
    /// Initializes the CSV report output.
    /// </summary>
    /// <param name="reporter">The method call reporter to be used.</param>
    /// <returns>An IAsyncDisposable that can be used to finalize the report.</returns>
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

    /// <summary>
    /// Sets the parameters for the CSV report output.
    /// </summary>
    /// <param name="parameter">The parameters to set.</param>
    public void SetParameters(object? parameter = null)
    {
        if (parameter is null)
        {
            return;
        }

        var parameters = (CsvReportParameters)parameter;
        _folderPath = parameters.FolderPath;
        _fileName = parameters.FileName;
        SetLimitOptions(parameters.LimitOptions);

        _methodOverrideManager = new MethodOverrideManager(_folderPath);
        _methodOverrideManager.LoadOverrides();

        _logger.LogInformation($"Parameters set: FolderPath = {_folderPath}, FileName = {_fileName}");
    }

    /// <summary>
    /// Writes a summary message to the report.
    /// </summary>
    /// <param name="message">The summary message to write.</param>
    public void WriteSummary(string message)
    {
        // Ignored in CSV output
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
            if (callStackItem is MethodCallStart methodCallStart)
            {
                reportItem.ItemName = methodCallStart.MethodCallInfo.MethodName;
                reportItem.MethodName = methodCallStart.MethodCallInfo.MethodName;
            }
        }
    }

    /// <summary>
    /// Writes an error to the report.
    /// </summary>
    /// <param name="exception">The exception to write.</param>
    public void WriteError(Exception exception)
    {
        _logger.LogError(exception, "Error occurred during CSV report generation");
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
            _logger.LogInformation($"Number of report items: {_helper.ReportItems.Count}");

            // Sort items by StartTime in descending order
            var sortedItems = _helper.ReportItems.OrderByDescending(item => DateTime.Parse(item.StartTime ?? DateTime.MinValue.ToString())).ToList();

            // Log details of items being exported
            for (int i = 0; i < sortedItems.Count; i++)
            {
                var item = sortedItems[i];
                _logger.LogDebug($"Exporting item {i}: {item.ItemName ?? item.MethodName}, MethodName: {item.MethodName}, StartTime: {item.StartTime}");
            }

            await using (var writer = new StreamWriter(fullPath, false, System.Text.Encoding.UTF8))
            {
                var csvReportWriter = new CsvReportWriter(writer, sortedItems, _methodOverrideManager);
                await csvReportWriter.WriteReportItemsCsvAsync();
            }

            _logger.LogInformation($"CSV report written to {fullPath} with {sortedItems.Count} items");

            // Verify file content
            var fileContent = await File.ReadAllTextAsync(fullPath);
            var lineCount = fileContent.Split('\n').Length;
            _logger.LogInformation($"Actual line count in file: {lineCount}");

            if (_limitOptions.MaxItems.HasValue)
            {
                _logger.LogInformation($"Output limited to {_limitOptions.MaxItems.Value} items");
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

    /// <summary>
    /// Sets the limit options for the CSV report output.
    /// </summary>
    /// <param name="options">The output limit options to set.</param>
    public void SetLimitOptions(OutputLimitOptions options)
    {
        _limitOptions = options;
        _helper.SetLimitOptions(options);
        _logger.LogInformation($"Limit options set: MaxItems = {options.MaxItems}");
    }

    /// <summary>
    /// Gets the current limit options for the CSV report output.
    /// </summary>
    /// <returns>The current output limit options.</returns>
    public OutputLimitOptions GetLimitOptions()
    {
        return _limitOptions;
    }

    /// <summary>
    /// Gets debug information about the current state of the CSV report output.
    /// </summary>
    /// <returns>A string containing debug information.</returns>
    public string GetDebugInfo() => _helper.GetDebugInfo();

    /// <summary>
    /// Gets the current count of report items.
    /// </summary>
    /// <returns>The number of report items.</returns>
    public int GetReportItemsCount()
    {
        return _helper.ReportItems.Count;
    }
}

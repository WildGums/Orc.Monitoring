namespace Orc.Monitoring.Reporters.ReportOutputs;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Orc.Monitoring.MethodLifeCycleItems;
using Orc.Monitoring.Reporters;
using Microsoft.Extensions.Logging;


public sealed class CsvReportOutput : IReportOutput, ILimitedOutput
{
    private readonly ILogger<CsvReportOutput> _logger = MonitoringController.CreateLogger<CsvReportOutput>();
    private readonly ReportOutputHelper _helper = new();
    private readonly Queue<ReportItem> _reportItems = new();

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
        _helper.Initialize(reporter);

        return new AsyncDisposable(async () => ExportToCsv());
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

        _methodOverrideManager = new(_folderPath);
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
            AddReportItem(reportItem);
        }
    }

    public void WriteError(Exception exception)
    {
        // Ignored in CSV output
    }

    private void AddReportItem(ReportItem item)
    {
        _reportItems.Enqueue(item);
        if (_limitOptions.MaxItems.HasValue && _reportItems.Count > _limitOptions.MaxItems.Value)
        {
            _reportItems.Dequeue();
        }
    }

    private void ExportToCsv()
    {
        if (_methodOverrideManager is null || _folderPath is null || _fileName is null)
        {
            return;
        }

        Directory.CreateDirectory(_folderPath);

        var fullPath = Path.Combine(_folderPath, $"{_fileName}.csv");

        try
        {
            using var writer = new StreamWriter(fullPath, false, Encoding.UTF8);
            var csvReportWriter = new CsvReportWriter(writer, _reportItems, _methodOverrideManager);
            csvReportWriter.WriteReportItemsCsv();

            _logger.LogInformation($"CSV report written to {fullPath} with {_reportItems.Count} lines");

            if (_limitOptions.MaxItems.HasValue)
            {
                _logger.LogInformation($"Output limited to {_limitOptions.MaxItems.Value} items");
            }
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, $"Error writing to CSV file: {ex.Message}");
        }

        ReportArchiver.CreateTimestampedFileCopy(fullPath);
    }

    public void SetLimitOptions(OutputLimitOptions options)
    {
        _limitOptions = options;
        if (options.MaxItems.HasValue)
        {
            while (_reportItems.Count > options.MaxItems.Value)
            {
                _reportItems.Dequeue();
            }
        }
    }

    public OutputLimitOptions GetLimitOptions()
    {
        return _limitOptions;
    }
}

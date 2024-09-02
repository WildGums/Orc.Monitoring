namespace Orc.Monitoring.Reporters.ReportOutputs;

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IO;
using Microsoft.Extensions.Logging;
using Orc.Monitoring.MethodLifeCycleItems;
using Orc.Monitoring.Reporters;

public sealed class CsvReportOutput : IReportOutput, ILimitableOutput
{
    private readonly ILogger<CsvReportOutput> _logger;
    private readonly ReportOutputHelper _helper;
    private readonly Func<string, MethodOverrideManager> _methodOverrideManagerFactory;
    private readonly IFileSystem _fileSystem;
    private readonly ReportArchiver _reportArchiver;

    private string? _fileName;
    private string? _folderPath;
    private MethodOverrideManager? _methodOverrideManager;
    private OutputLimitOptions _limitOptions = OutputLimitOptions.Unlimited;

    public CsvReportOutput()
    : this(MonitoringLoggerFactory.Instance, new ReportOutputHelper(MonitoringLoggerFactory.Instance),
        (outputFolder) => new MethodOverrideManager(outputFolder, MonitoringLoggerFactory.Instance, FileSystem.Instance, CsvUtils.Instance),
        FileSystem.Instance, new ReportArchiver(FileSystem.Instance))
    {

    }

    public CsvReportOutput(IMonitoringLoggerFactory loggerFactory, ReportOutputHelper reportOutputHelper, Func<string, MethodOverrideManager> methodOverrideManagerFactory,
        IFileSystem fileSystem, ReportArchiver reportArchiver)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(reportOutputHelper);
        ArgumentNullException.ThrowIfNull(methodOverrideManagerFactory);
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(reportArchiver);

        _logger = loggerFactory.CreateLogger<CsvReportOutput>();
        _helper = reportOutputHelper;
        _methodOverrideManagerFactory = methodOverrideManagerFactory;
        _fileSystem = fileSystem;
        _reportArchiver = reportArchiver;

        _logger.LogDebug($"Created {nameof(CsvReportOutput)}");
    }

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

        EnsureDirectoryWritable();

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

    private void EnsureDirectoryWritable()
    {
        if (_folderPath is null)
        {
            throw new InvalidOperationException("Folder path is not set");
        }

        var testFilePath = Path.Combine(_folderPath, "test_write.tmp");
        try
        {
            _fileSystem.WriteAllText(testFilePath, "Test");
            _fileSystem.DeleteFile(testFilePath);
        }
        catch (UnauthorizedAccessException)
        {
            throw new UnauthorizedAccessException("Access to the path is denied.");
        }
    }

    public void SetParameters(object? parameter = null)
    {
        ArgumentNullException.ThrowIfNull(parameter, "Parameter cannot be null");

        var parameters = (CsvReportParameters)parameter;

        _folderPath = parameters.FolderPath;
        ArgumentNullException.ThrowIfNull(_folderPath, "FolderPath cannot be null");

        _fileName = parameters.FileName;
        ArgumentNullException.ThrowIfNull(_fileName, "FileName cannot be null");

        SetLimitOptions(parameters.LimitOptions);

        _methodOverrideManager = _methodOverrideManagerFactory(_folderPath);
        _methodOverrideManager.ReadOverrides();

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
        }
    }

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

        _fileSystem.CreateDirectory(_folderPath);

        var fullPath = Path.Combine(_folderPath, $"{_fileName}.csv");

        try
        {
            _logger.LogInformation($"Starting CSV export to {fullPath}");
            _logger.LogInformation($"Number of report items: {_helper.ReportItems.Count}");

            var sortedItems = _helper.ReportItems
                .OrderByDescending(item => DateTime.Parse(item.StartTime ?? DateTime.MinValue.ToString()))
                .Take(_limitOptions.MaxItems ?? int.MaxValue)
                .ToList();

            for (int i = 0; i < sortedItems.Count; i++)
            {
                var item = sortedItems[i];
                _logger.LogDebug($"Exporting item {i}: {item.ItemName ?? item.MethodName}, MethodName: {item.MethodName}, StartTime: {item.StartTime}");
            }

            TextWriter? writer = null;
            try
            {
                writer = _fileSystem.CreateStreamWriter(fullPath, false, System.Text.Encoding.UTF8);
                var csvReportWriter = new CsvReportWriter(writer, sortedItems, _methodOverrideManager);
                await csvReportWriter.WriteReportItemsCsvAsync();
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, $"Error creating or writing to CSV file: {ex.Message}");
                throw;
            }
            finally
            {
                writer?.Dispose();
            }

            _logger.LogInformation($"CSV report written to {fullPath} with {sortedItems.Count} items");

            // Verify file content
            var fileContent = await _fileSystem.ReadAllTextAsync(fullPath);
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
            _reportArchiver.CreateTimestampedFileCopy(fullPath);
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
        _helper.SetLimitOptions(options);
        _logger.LogInformation($"Limit options set: MaxItems = {options.MaxItems}");
    }

    public OutputLimitOptions GetLimitOptions()
    {
        return _limitOptions;
    }

    public string GetDebugInfo() => _helper.GetDebugInfo();

    public int GetReportItemsCount()
    {
        return _helper.ReportItems.Count;
    }
}

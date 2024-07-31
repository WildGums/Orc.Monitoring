namespace Orc.Monitoring.Reporters.ReportOutputs;

using System;
using System.IO;
using System.Linq;
using MethodLifeCycleItems;
using Reporters;

public sealed class CsvReportOutput : IReportOutput
{
    private readonly ReportOutputHelper _helper = new();

    private string? _fileName;
    private string? _folderPath;
    private MethodOverrideManager? _methodOverrideManager;

    public static CsvReportParameters CreateParameters(string folderPath, string fileName)
    {
        ArgumentNullException.ThrowIfNull(folderPath);
        ArgumentNullException.ThrowIfNull(fileName);

        return new()
        {
            FolderPath = folderPath,
            FileName = fileName
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

        _methodOverrideManager = new(_folderPath);
    }

    public void WriteSummary(string message)
    {
        // Ignored in CSV output
    }

    public void WriteItem(ICallStackItem callStackItem, string? message = null)
    {
        _helper.ProcessCallStackItem(callStackItem);
    }

    public void WriteError(Exception exception)
    {
        // Ignored in CSV output
    }

    private void ExportToCsv()
    {
        if (_folderPath is null || _fileName is null)
        {
            return;
        }

        if (!Directory.Exists(_folderPath))
        {
            Directory.CreateDirectory(_folderPath);
        }

        var fullPath = Path.Combine(_folderPath, $"{_fileName}.csv");

        var reportItems = _helper.ReportItems.Values.Concat(_helper.Gaps);
        var csvReportWriter = new CsvReportWriter(fullPath, reportItems, _methodOverrideManager);
        csvReportWriter.WriteReportItemsCsv();

        ReportArchiver.CreateTimestampedFileCopy(fullPath);
    }
}

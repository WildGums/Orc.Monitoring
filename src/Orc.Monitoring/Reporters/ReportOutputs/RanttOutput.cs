namespace Orc.Monitoring.Reporters.ReportOutputs;

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orc.Monitoring.MethodLifeCycleItems;
using Orc.Monitoring.Reporters;

public class RanttOutput : IReportOutput, ILimitedOutput
{
    private const string RanttProjectContents = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Project RanttVersion=""3.5.0.0"" MinimumRanttVersion=""2.0"">
  <General Name=""%reportName%"" Description=""%reportName%"" DefaultAttribute=""Resource"">
    <Culture BaseCulture=""en-AU"" FirstDayOfWeek=""Monday"" />
    <DateRange IsEnabled=""false"" />
    <RefreshInterval IsEnabled=""false"" Interval=""0"" UnitOfTime=""Second"" />
  </General>
  <DataSets>
    <DataSet Name=""%reportName%"">
      <Operations IsEmpty=""false"" SourceType=""Csv"" Source=""%SourceFileName%"" DateRepresentation=""Absolute"">
        <FieldMappings>
          <FieldMapping From=""MethodName"" To=""Resource"" />
          <FieldMapping From=""Id"" To=""Reference"" />
        </FieldMappings>
      </Operations>
      <CalendarPeriods IsEmpty=""true"" SourceType=""Csv"" Source="""" DateRepresentation=""Absolute"">
        <FieldMappings />
      </CalendarPeriods>
      <Relationships IsEmpty=""false"" SourceType=""Csv"" Source=""%RelationshipsFileName%"" DateRepresentation=""Absolute"">
        <FieldMappings>
          <FieldMapping From=""From"" To=""From"" />
          <FieldMapping From=""To"" To=""To"" />
        </FieldMappings>
      </Relationships>
    </DataSet>
  </DataSets>
</Project>";

    private readonly ILogger<RanttOutput> _logger = MonitoringController.CreateLogger<RanttOutput>();
    private readonly ReportOutputHelper _helper = new();

    private string? _folderPath;
    private string? _outputDirectory;
    private MethodOverrideManager? _overrideManager;

    public static RanttReportParameters CreateParameters(string folderPath, OutputLimitOptions? limitOptions = null) => new()
    {
        FolderPath = folderPath,
        LimitOptions = limitOptions ?? OutputLimitOptions.Unlimited
    };

    public IAsyncDisposable Initialize(IMethodCallReporter reporter)
    {
        _helper.Initialize(reporter);

        return new AsyncDisposable(async () =>
        {
            var reporterName = _helper.Reporter?.FullName;
            if (reporterName is null)
            {
                throw new InvalidOperationException("Reporter name is not set");
            }

            if (_folderPath is null)
            {
                throw new InvalidOperationException("Folder path is not set");
            }

            _outputDirectory = Path.Combine(_folderPath, reporterName);

            if (!Directory.Exists(_outputDirectory))
            {
                Directory.CreateDirectory(_outputDirectory);
            }

            _overrideManager = new MethodOverrideManager(_outputDirectory);
            _overrideManager.LoadOverrides();

            await ExportDataAsync();

            _overrideManager.SaveOverrides(_helper.ReportItems);

            await CreateTimestampedFolderCopyAsync(_outputDirectory);
        });
    }

    public void SetParameters(object? parameter = null)
    {
        if (parameter is null)
        {
            return;
        }

        var parameters = (RanttReportParameters)parameter;
        _folderPath = parameters.FolderPath;
        SetLimitOptions(parameters.LimitOptions);
    }

    public void WriteSummary(string message)
    {
        // Ignored in Rantt output
    }

    public void WriteItem(ICallStackItem callStackItem, string? message = null)
    {
        _helper.ProcessCallStackItem(callStackItem);
    }

    public void WriteError(Exception exception)
    {
        // Ignored in Rantt output
    }

    private async Task ExportDataAsync()
    {
        if (_outputDirectory is null)
        {
            throw new InvalidOperationException("Output directory is not set");
        }

        if (_helper.Reporter?.FullName is null)
        {
            throw new InvalidOperationException("Reporter name is not set");
        }

        var reporterName = _helper.Reporter.FullName;
        var fileName = $"{reporterName}.csv";
        var relationshipsFileName = $"{reporterName}_Relationships.csv";

        await ExportToCsvAsync(fileName);
        await ExportRelationshipsToCsvAsync(relationshipsFileName);
        ExportToRantt(reporterName, fileName, relationshipsFileName);

        _logger.LogInformation($"Rantt data exported with {_helper.ReportItems.Count} items");
        var limitOptions = GetLimitOptions();
        if (limitOptions.MaxItems.HasValue)
        {
            _logger.LogInformation($"Output limited to {limitOptions.MaxItems.Value} items");
        }
        if (limitOptions.MaxAge.HasValue)
        {
            _logger.LogInformation($"Output limited to items newer than {limitOptions.MaxAge.Value}");
        }
    }

    private async Task ExportToCsvAsync(string fileName)
    {
        if (_outputDirectory is null || _overrideManager is null)
        {
            throw new InvalidOperationException("Output directory or method override manager is not set");
        }

        var fullPath = Path.Combine(_outputDirectory, fileName);

        try
        {
            using var writer = new StreamWriter(fullPath, false, Encoding.UTF8);
            var csvReportWriter = new CsvReportWriter(writer, _helper.ReportItems, _overrideManager);
            await csvReportWriter.WriteReportItemsCsvAsync();

            _logger.LogInformation($"CSV report written to {fullPath} with {_helper.ReportItems.Count} items");
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, $"Error writing to CSV file: {ex.Message}");
        }
    }

    private async Task ExportRelationshipsToCsvAsync(string fileName)
    {
        if (_outputDirectory is null || _overrideManager is null)
        {
            throw new InvalidOperationException("Output directory or method override manager is not set");
        }

        var fullPath = Path.Combine(_outputDirectory, fileName);

        try
        {
            using var writer = new StreamWriter(fullPath, false, Encoding.UTF8);
            var csvReportWriter = new CsvReportWriter(writer, _helper.ReportItems, _overrideManager);
            await csvReportWriter.WriteRelationshipsCsvAsync();
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, $"Error writing to relationships CSV file: {ex.Message}");
        }
    }

    private void ExportToRantt(string reporterName, string dataFileName, string relationshipsFileName)
    {
        if (_outputDirectory is null)
        {
            throw new InvalidOperationException("Output directory is not set");
        }

        var ranttProjectFileName = $"{reporterName}.rprjx";
        var ranttProjectContents = RanttProjectContents
            .Replace("%SourceFileName%", dataFileName)
            .Replace("%RelationshipsFileName%", relationshipsFileName)
            .Replace("%reportName%", reporterName);
        var ranttProjectPath = Path.Combine(_outputDirectory, ranttProjectFileName);

        File.WriteAllText(ranttProjectPath, ranttProjectContents);
    }

    private async Task CreateTimestampedFolderCopyAsync(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            return;
        }

        var directory = Path.GetDirectoryName(folderPath);
        if (directory is null)
        {
            return;
        }

        var archiveDirectory = GetArchiveDirectory(directory);

        var folderName = Path.GetFileName(folderPath);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var archivedFolderName = $"{folderName}_{timestamp}";
        var archivedFolderPath = Path.Combine(archiveDirectory, archivedFolderName);

        await CopyFolderAsync(folderPath, archivedFolderPath);
    }

    private static string GetArchiveDirectory(string directory)
    {
        var archiveDirectory = Path.Combine(directory, "Archived");
        if (!Directory.Exists(archiveDirectory))
        {
            Directory.CreateDirectory(archiveDirectory);
        }

        return archiveDirectory;
    }

    private static async Task CopyFolderAsync(string sourcePath, string destinationPath)
    {
        if (!Directory.Exists(sourcePath))
        {
            return;
        }

        if (!Directory.Exists(destinationPath))
        {
            Directory.CreateDirectory(destinationPath);
        }

        foreach (var file in Directory.GetFiles(sourcePath))
        {
            var fileName = Path.GetFileName(file);
            var destinationFilePath = Path.Combine(destinationPath, fileName);
            await CopyFileAsync(file, destinationFilePath);
        }

        foreach (var subFolder in Directory.GetDirectories(sourcePath))
        {
            var folderName = Path.GetFileName(subFolder);
            var destinationSubFolderPath = Path.Combine(destinationPath, folderName);
            await CopyFolderAsync(subFolder, destinationSubFolderPath);
        }
    }

    private static async Task CopyFileAsync(string sourcePath, string destinationPath)
    {
        using var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var destinationStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await sourceStream.CopyToAsync(destinationStream);
    }

    public void SetLimitOptions(OutputLimitOptions options)
    {
        _helper.SetLimitOptions(options);
    }

    public OutputLimitOptions GetLimitOptions()
    {
        return _helper.GetLimitOptions();
    }
}

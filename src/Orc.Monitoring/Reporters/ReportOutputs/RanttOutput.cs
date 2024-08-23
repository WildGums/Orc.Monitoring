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
    private readonly List<ReportItem> _reportItems = new();

    private string? _folderPath;
    private string? _outputDirectory;
    private MethodOverrideManager? _overrideManager;
    private OutputLimitOptions _limitOptions = OutputLimitOptions.Unlimited;

    public static RanttReportParameters CreateParameters(string folderPath, OutputLimitOptions? limitOptions = null) => new()
    {
        FolderPath = folderPath,
        LimitOptions = limitOptions ?? OutputLimitOptions.Unlimited
    };

    public IAsyncDisposable Initialize(IMethodCallReporter reporter)
    {
        _logger.LogInformation($"Initializing {nameof(RanttOutput)}");
        _helper.Initialize(reporter);

        return new AsyncDisposable(async () =>
        {
            _logger.LogInformation($"Disposing {nameof(RanttOutput)}");
            try
            {
                await ExportDataAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error during {nameof(RanttOutput)} export");
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

        var parameters = (RanttReportParameters)parameter;
        _folderPath = parameters.FolderPath;
        SetLimitOptions(parameters.LimitOptions);

        _logger.LogInformation($"Parameters set: FolderPath = {_folderPath}");
    }

    public void WriteSummary(string message)
    {
        // Ignored in Rantt output
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
        _logger.LogError(exception, "Error occurred during Rantt report generation");
    }

    private void AddReportItem(ReportItem item)
    {
        _reportItems.Add(item);
        ApplyLimits();
        _logger.LogDebug($"Added report item. Current count: {_reportItems.Count}");
    }

    private void ApplyLimits()
    {
        if (_limitOptions.MaxAge.HasValue)
        {
            var cutoffTime = DateTime.Now - _limitOptions.MaxAge.Value;
            _reportItems.RemoveAll(r => DateTime.Parse(r.StartTime ?? DateTime.MinValue.ToString()) < cutoffTime);
        }
        if (_limitOptions.MaxItems.HasValue)
        {
            _reportItems.Sort((a, b) => string.Compare(b.StartTime, a.StartTime));
            while (_reportItems.Count > _limitOptions.MaxItems.Value)
            {
                _reportItems.RemoveAt(_reportItems.Count - 1);
            }
        }
    }

    private async Task ExportDataAsync()
    {
        if (_folderPath is null)
        {
            throw new InvalidOperationException("Folder path is not set");
        }

        var reporter = _helper.Reporter;
        if (reporter is null)
        {
            throw new InvalidOperationException("Reporter is not set");
        }

        _outputDirectory = Path.Combine(_folderPath, reporter.FullName);

        Directory.CreateDirectory(_outputDirectory);
        _logger.LogInformation($"Created output directory: {_outputDirectory}");

        _overrideManager = new MethodOverrideManager(_outputDirectory);
        _overrideManager.LoadOverrides();

        var fileName = $"{reporter.FullName}.csv";
        var relationshipsFileName = $"{reporter.FullName}_Relationships.csv";

        try
        {
            _logger.LogInformation("Starting Rantt data export");
            await ExportToCsvAsync(fileName);
            await ExportRelationshipsToCsvAsync(relationshipsFileName);
            ExportToRantt(reporter.FullName, fileName, relationshipsFileName);

            _logger.LogInformation($"Rantt data exported with {_reportItems.Count} items");

            if (_limitOptions.MaxItems.HasValue)
            {
                _logger.LogInformation($"Output limited to {_limitOptions.MaxItems.Value} items");
            }
            if (_limitOptions.MaxAge.HasValue)
            {
                _logger.LogInformation($"Output limited to items newer than {_limitOptions.MaxAge.Value}");
            }

            _overrideManager.SaveOverrides(_reportItems);

            await CreateTimestampedFolderCopyAsync(_outputDirectory);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting Rantt data");
            throw;
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
            _logger.LogInformation($"Starting CSV export to {fullPath}");
            _logger.LogInformation($"Number of report items: {_reportItems.Count}");

            await using (var writer = new StreamWriter(fullPath, false, Encoding.UTF8))
            {
                var sortedItems = _reportItems.OrderByDescending(item => item.StartTime);
                var csvReportWriter = new CsvReportWriter(writer, sortedItems, _overrideManager);
                await csvReportWriter.WriteReportItemsCsvAsync();
            }

            _logger.LogInformation($"CSV report written to {fullPath} with {_reportItems.Count} items");

            // Add a small delay to ensure the file is fully written and closed
            await Task.Delay(100);

            // Verify file content
            var fileContent = await File.ReadAllTextAsync(fullPath);
            var lineCount = fileContent.Split('\n').Length;
            _logger.LogInformation($"Actual line count in file: {lineCount}");
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, $"Error writing to CSV file: {ex.Message}");
            throw;
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
            _logger.LogInformation($"Starting relationships CSV export to {fullPath}");
            await using (var writer = new StreamWriter(fullPath, false, Encoding.UTF8))
            {
                await writer.WriteLineAsync("From,To,RelationType");
                foreach (var item in _reportItems.Where(r => r.Parent is not null))
                {
                    var relationType = DetermineRelationType(item);
                    await writer.WriteLineAsync($"{item.Parent},{item.Id},{relationType}");
                    _logger.LogDebug($"Writing relationship: {item.Parent} -> {item.Id} ({relationType})");
                }
            }
            _logger.LogInformation($"Relationships CSV written to {fullPath}");
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, $"Error writing to relationships CSV file: {ex.Message}");
            throw;
        }
    }

    private string DetermineRelationType(ReportItem item)
    {
        if (bool.TryParse(item.Parameters.GetValueOrDefault("IsStatic"), out bool isStatic) && isStatic)
            return "Static";
        if (bool.TryParse(item.Parameters.GetValueOrDefault("IsExtension"), out bool isExtension) && isExtension)
            return "Extension";
        if (bool.TryParse(item.Parameters.GetValueOrDefault("IsGeneric"), out bool isGeneric) && isGeneric)
            return "Generic";
        return "Regular";
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

        try
        {
            _logger.LogInformation($"Writing Rantt project file to {ranttProjectPath}");
            File.WriteAllText(ranttProjectPath, ranttProjectContents);
            _logger.LogInformation("Rantt project file written successfully");
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, $"Error writing Rantt project file: {ex.Message}");
            throw;
        }
    }

    private async Task CreateTimestampedFolderCopyAsync(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            _logger.LogWarning($"Folder does not exist: {folderPath}");
            return;
        }

        var directory = Path.GetDirectoryName(folderPath);
        if (directory is null)
        {
            _logger.LogWarning("Unable to get directory name");
            return;
        }

        var archiveDirectory = GetArchiveDirectory(directory);

        var folderName = Path.GetFileName(folderPath);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var archivedFolderName = $"{folderName}_{timestamp}";
        var archivedFolderPath = Path.Combine(archiveDirectory, archivedFolderName);

        try
        {
            _logger.LogInformation($"Creating timestamped folder copy: {archivedFolderPath}");
            await CopyFolderAsync(folderPath, archivedFolderPath);
            _logger.LogInformation("Timestamped folder copy created successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating timestamped folder copy");
        }
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

    private async Task CopyFolderAsync(string sourcePath, string destinationPath)
    {
        if (!Directory.Exists(sourcePath))
        {
            return;
        }

        Directory.CreateDirectory(destinationPath);

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

    private async Task CopyFileAsync(string sourcePath, string destinationPath)
    {
        using var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var destinationStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await sourceStream.CopyToAsync(destinationStream);
    }

    public void SetLimitOptions(OutputLimitOptions options)
    {
        _limitOptions = options;
        ApplyLimits();
        _helper.SetLimitOptions(options);
    }

    public OutputLimitOptions GetLimitOptions()
    {
        return _helper.GetLimitOptions();
    }

    public string GetDebugInfo() => _helper.GetDebugInfo();
}

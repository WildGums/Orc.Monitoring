namespace Orc.Monitoring.Reporters.ReportOutputs;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IO;
using Microsoft.Extensions.Logging;
using Orc.Monitoring;
using Orc.Monitoring.MethodLifeCycleItems;
using Orc.Monitoring.Reporters;

public sealed class RanttOutput : IReportOutput, ILimitableOutput
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

    private readonly ILogger<RanttOutput> _logger;
    private readonly ReportOutputHelper _helper;
    private string? _folderPath;
    private string? _outputDirectory;
    private MethodOverrideManager? _overrideManager;
    private OutputLimitOptions _limitOptions = OutputLimitOptions.Unlimited;
    private readonly Func<IEnhancedDataPostProcessor> _enhancedDataPostProcessorFactory;
    private readonly Func<string, MethodOverrideManager> _methodOverrideManagerFactory;
    private readonly IFileSystem _fileSystem;
    private readonly ReportArchiver _reportArchiver;
    private readonly CsvUtils _csvUtils;

    public RanttOutput()
    : this(MonitoringLoggerFactory.Instance, () => new EnhancedDataPostProcessor(MonitoringLoggerFactory.Instance),
            new ReportOutputHelper(MonitoringLoggerFactory.Instance), (outputFolder) => new MethodOverrideManager(outputFolder, MonitoringLoggerFactory.Instance, FileSystem.Instance, CsvUtils.Instance),
            FileSystem.Instance, new ReportArchiver(FileSystem.Instance))
    {

    }

    public RanttOutput(IMonitoringLoggerFactory monitoringLoggerFactory, Func<IEnhancedDataPostProcessor> enhancedDataPostProcessorFactory, ReportOutputHelper reportOutputHelper,
        Func<string, MethodOverrideManager> methodOverrideManagerFactory, IFileSystem fileSystem, ReportArchiver reportArchiver)
    {
        _logger = monitoringLoggerFactory.CreateLogger<RanttOutput>();
        _enhancedDataPostProcessorFactory = enhancedDataPostProcessorFactory;
        _helper = reportOutputHelper;
        _methodOverrideManagerFactory = methodOverrideManagerFactory;
        _fileSystem = fileSystem;
        _reportArchiver = reportArchiver;
        _csvUtils = new CsvUtils(_fileSystem);
    }

    public static RanttReportParameters CreateParameters(
        string folderPath,
        OutputLimitOptions? limitOptions = null) => new()
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
                await ExportDataAsync(reporter);
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
        ArgumentNullException.ThrowIfNull(parameter, "Parameters cannot be null");

        var parameters = (RanttReportParameters)parameter;

        _folderPath = parameters.FolderPath;
        ArgumentNullException.ThrowIfNull(_folderPath, "FolderPath cannot be null");

        SetLimitOptions(parameters.LimitOptions);

        _overrideManager = _methodOverrideManagerFactory(_folderPath);

        _logger.LogInformation($"Parameters set: FolderPath = {_folderPath}");
    }

    public void WriteSummary(string message)
    {
        // Ignored in Rantt output
    }

    public void WriteItem(ICallStackItem callStackItem, string? message = null)
    {
        _logger.LogInformation($"WriteItem called with callStackItem type: {callStackItem.GetType().Name}");
        var reportItem = _helper.ProcessCallStackItem(callStackItem);
        if (reportItem is not null)
        {
            if (callStackItem is MethodCallStart methodCallStart)
            {
                _logger.LogInformation($"Processing MethodCallStart: Id={methodCallStart.MethodCallInfo.Id}, MethodName={methodCallStart.MethodCallInfo.MethodName}, Parent={methodCallStart.MethodCallInfo.Parent?.Id ?? "ROOT"}");
                reportItem.MethodName = methodCallStart.MethodCallInfo.MethodName;
                reportItem.FullName = $"{methodCallStart.MethodCallInfo.ClassType?.Name}.{methodCallStart.MethodCallInfo.MethodName}";
                reportItem.Parent = methodCallStart.MethodCallInfo.Parent?.Id ?? "ROOT";

                var parameters = new Dictionary<string, string>(reportItem.Parameters);
                foreach (var param in methodCallStart.MethodCallInfo.Parameters ?? [])
                {
                    parameters[param.Key] = param.Value;
                }
                reportItem.Parameters = parameters;
            }
            else if (callStackItem is MethodCallEnd methodCallEnd)
            {
                _logger.LogInformation($"Processing MethodCallEnd: Id={methodCallEnd.MethodCallInfo.Id}, MethodName={methodCallEnd.MethodCallInfo.MethodName}");
            }
            _logger.LogInformation($"Processed item: Id={reportItem.Id}, MethodName={reportItem.MethodName}, FullName={reportItem.FullName}, Parent={reportItem.Parent}");
        }
        else
        {
            _logger.LogWarning($"ProcessCallStackItem returned null for {callStackItem.GetType().Name}");
        }
    }

    public void WriteError(Exception exception)
    {
        _logger.LogError(exception, "Error occurred during Rantt report generation");
    }

    private async Task ExportDataAsync(IMethodCallReporter reporter)
    {
        if (_folderPath is null)
        {
            throw new InvalidOperationException("Folder path is not set");
        }

        _outputDirectory = Path.Combine(_folderPath, reporter.FullName);

        _fileSystem.CreateDirectory(_outputDirectory);
        _logger.LogInformation($"Created output directory: {_outputDirectory}");

        if (_overrideManager is null)
        {
            _overrideManager = _methodOverrideManagerFactory(_outputDirectory!);
        }
        _overrideManager.ReadOverrides();

        var fileName = $"{reporter.FullName}.csv";
        var relationshipsFileName = $"{reporter.FullName}_Relationships.csv";

        try
        {
            _logger.LogInformation("Starting Rantt data export");
            await ExportToCsvAsync(fileName, reporter);
            await ExportRelationshipsToCsvAsync(relationshipsFileName);
            ExportToRantt(reporter, fileName, relationshipsFileName);

            _logger.LogInformation($"Rantt data exported with {_helper.ReportItems.Count} items");

            if (_limitOptions.MaxItems.HasValue)
            {
                _logger.LogInformation($"Output limited to {_limitOptions.MaxItems.Value} items");
            }

            _overrideManager.SaveOverrides(_helper.ReportItems.ToList());

            await _reportArchiver.CreateTimestampedFolderCopyAsync(_outputDirectory);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting Rantt data");
            throw;
        }
    }

    private async Task ExportToCsvAsync(string fileName, IMethodCallReporter reporter)
    {
        if (_outputDirectory is null || _overrideManager is null)
        {
            throw new InvalidOperationException("Output directory or method override manager is not set");
        }

        var fullPath = Path.Combine(_outputDirectory, fileName);

        try
        {
            _logger.LogInformation($"Starting CSV export to {fullPath}");
            _logger.LogInformation($"Number of report items before processing: {_helper.ReportItems.Count}");

            var sortedItems = _helper.ReportItems
                .OrderBy(item => DateTime.Parse(item.StartTime ?? DateTime.MinValue.ToString()))
                .ToList();

            _logger.LogInformation($"Number of sorted items: {sortedItems.Count}");
            foreach (var item in sortedItems)
            {
                _logger.LogInformation($"Sorted item: Id={item.Id}, MethodName={item.MethodName}");
            }

            var enhancedDataPostProcessor = _enhancedDataPostProcessorFactory();
            var processedItems = enhancedDataPostProcessor.PostProcessData(sortedItems);

            _logger.LogInformation($"Number of items after post-processing: {processedItems.Count}");
            foreach (var item in processedItems)
            {
                _logger.LogInformation($"Processed item: Id={item.Id}, MethodName={item.MethodName}");
            }

            var itemsToWrite = processedItems.Count > 0 ? processedItems : sortedItems;
            // Ensure there's at least one item if the test added one
            if (itemsToWrite.Count == 0 && _helper.ReportItems.Count > 0)
            {
                itemsToWrite = (List<ReportItem>)_helper.ReportItems;
            }

            var itemsWithOverrides = itemsToWrite.Select(item =>
            {
                var fullName = item.Parameters.TryGetValue("FullName", out var fn) ? fn : item.FullName ?? string.Empty;
                var overrides = _overrideManager.GetOverridesForMethod(fullName);
                var newParameters = new Dictionary<string, string>(item.Parameters, StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in overrides)
                {
                    newParameters[kvp.Key] = kvp.Value;
                }
                return new ReportItem
                {
                    Id = item.Id,
                    StartTime = item.StartTime,
                    ItemName = item.ItemName,
                    EndTime = item.EndTime,
                    Duration = item.Duration,
                    Report = item.Report,
                    ThreadId = item.ThreadId,
                    Level = item.Level,
                    ClassName = item.ClassName,
                    MethodName = item.MethodName,
                    FullName = fullName,
                    Parent = item.Parent,
                    ParentThreadId = item.ParentThreadId,
                    Parameters = newParameters,
                    AttributeParameters = new HashSet<string>(item.AttributeParameters)
                };
            }).ToList();

            _logger.LogInformation($"Number of items with overrides: {itemsWithOverrides.Count}");

            await using (var writer = _fileSystem.CreateStreamWriter(fullPath, false, Encoding.UTF8))
            {
                var csvReportWriter = new CsvReportWriter(writer, itemsWithOverrides, _overrideManager);
                await csvReportWriter.WriteReportItemsCsvAsync();
            }

            _logger.LogInformation($"CSV report written to {fullPath} with {itemsWithOverrides.Count} items");

            var fileContent = await _fileSystem.ReadAllTextAsync(fullPath);
            var lineCount = fileContent.Split('\n').Length;
            _logger.LogInformation($"Actual line count in file: {lineCount}");
            _logger.LogDebug($"File content:\n{fileContent}");
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

            var sortedItems = _helper.ReportItems
                .OrderByDescending(item => DateTime.Parse(item.StartTime ?? DateTime.MinValue.ToString()))
                .ToList();

            var enhancedDataPostProcessor = _enhancedDataPostProcessorFactory();
            var processedItems = enhancedDataPostProcessor.PostProcessData(sortedItems);

            await using (var writer = _fileSystem.CreateStreamWriter(fullPath, false, Encoding.UTF8))
            {
                await writer.WriteLineAsync("From,To,RelationType");
                foreach (var item in processedItems.Where(r => r.Parent is not null))
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

    private string EscapeXmlContent(string content)
    {
        return System.Security.SecurityElement.Escape(content) ?? string.Empty;
    }

    private void ExportToRantt(IMethodCallReporter reporter, string dataFileName, string relationshipsFileName)
    {
        if (_outputDirectory is null)
        {
            throw new InvalidOperationException("Output directory is not set");
        }

        var ranttProjectFileName = $"{EscapeXmlContent(reporter.FullName)}.rprjx";
        var ranttProjectContents = RanttProjectContents
            .Replace("%SourceFileName%", EscapeXmlContent(dataFileName))
            .Replace("%RelationshipsFileName%", EscapeXmlContent(relationshipsFileName))
            .Replace("%reportName%", EscapeXmlContent(reporter.FullName));
        var ranttProjectPath = Path.Combine(_outputDirectory, ranttProjectFileName);

        try
        {
            _logger.LogInformation($"Writing Rantt project file to {ranttProjectPath}");
            _fileSystem.WriteAllText(ranttProjectPath, ranttProjectContents);
            _logger.LogInformation("Rantt project file written successfully");
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, $"Error writing Rantt project file: {ex.Message}");
            throw;
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
}

namespace Orc.Monitoring.Reporters.ReportOutputs;

using System;
using System.IO;
using System.Linq;
using MethodLifeCycleItems;
using Reporters;


public class RanttOutput : IReportOutput
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

    private readonly ReportOutputHelper _helper = new();

    private string? _folderPath;
    private string? _outputDirectory;

    private MethodOverrideManager? _overrideManager;

    public static RanttReportParameters CreateParameters(string folderPath) => new()
    {
        FolderPath = folderPath
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

            ExportToCsv();
            ExportRelationshipsToCsv();
            ExportToRantt();

            _overrideManager.SaveOverrides(_helper.ReportItems.Values);

            ReportArchiver.CreateTimestampedFolderCopy(_outputDirectory);
        });
    }

    public void SetParameters(object? parameter = null)
    {
        if (parameter is null)
        {
            return;
        }

        var parameters = (RanttReportParameters)parameter;
        _folderPath = parameters?.FolderPath;
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

    private void ExportToCsv()
    {
        if (_outputDirectory is null)
        {
            throw new InvalidOperationException("Output directory is not set");
        }

        if (!Directory.Exists(_outputDirectory))
        {
            Directory.CreateDirectory(_outputDirectory);
        }

        if(_overrideManager is null)
        {
            throw new InvalidOperationException("Method override manager is not set");
        }

        var reporterName = _helper.Reporter?.FullName;
        if (reporterName is null)
        {
            throw new InvalidOperationException("Reporter name is not set");
        }

        var fileName = reporterName;
        var fullPath = Path.Combine(_outputDirectory, $"{fileName}.csv");

        var reportItems = _helper.ReportItems.Values.Concat(_helper.Gaps);
        var csvReportWriter = new CsvReportWriter(fullPath, reportItems, _overrideManager);
        csvReportWriter.WriteReportItemsCsv();
    }

    private void ExportRelationshipsToCsv()
    {
        if (_outputDirectory is null)
        {
            throw new InvalidOperationException("Output directory is not set");
        }

        if (!Directory.Exists(_outputDirectory)) 
        {
            Directory.CreateDirectory(_outputDirectory);
        }

        if(_overrideManager is null)
        {
            throw new InvalidOperationException("Method override manager is not set");
        }

        var reporterName = _helper.Reporter?.FullName;
        if (reporterName is null)
        {
            throw new InvalidOperationException("Reporter name is not set");
        }

        var fileName = $"{reporterName}_Relationships";
        var fullPath = Path.Combine(_outputDirectory, $"{fileName}.csv");

        var reportItems = _helper.ReportItems.Values;
        var csvReportWriter = new CsvReportWriter(fullPath, reportItems, _overrideManager);
        csvReportWriter.WriteRelationshipsCsv(fullPath);
    }

    private void ExportToRantt()
    {
        if (_outputDirectory is null)
        {
            throw new InvalidOperationException("Output directory is not set");
        }

        if (!Directory.Exists(_outputDirectory)) 
        {
            Directory.CreateDirectory(_outputDirectory);
        }

        if (_overrideManager is null)
        {
            throw new InvalidOperationException("Method override manager is not set");
        }

        var reporterName = _helper.Reporter?.FullName;
        if (reporterName is null)
        {
            throw new InvalidOperationException("Reporter name is not set");
        }

        var reportName = $"{reporterName}";
        var fileName = reportName;

        var ranttProjectFileName = $"{fileName}.rprjx";
        var ranttProjectContents = RanttProjectContents
            .Replace("%SourceFileName%", fileName + ".csv")
            .Replace("%RelationshipsFileName%", fileName + "_Relationships.csv")
            .Replace("%reportName%", reportName);
        var ranttProjectPath = Path.Combine(_outputDirectory, ranttProjectFileName);

        File.WriteAllText(ranttProjectPath, ranttProjectContents);
    }
}

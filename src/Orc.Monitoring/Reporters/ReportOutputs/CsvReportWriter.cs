namespace Orc.Monitoring.Reporters.ReportOutputs;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

public class CsvReportWriter
{
    private readonly string _filePath;
    private readonly IEnumerable<ReportItem> _reportItems;
    private readonly MethodOverrideManager _overrideManager;
    private readonly HashSet<string> _customColumns;

    public CsvReportWriter(string filePath, IEnumerable<ReportItem> reportItems, MethodOverrideManager? overrideManager)
    {
        ArgumentNullException.ThrowIfNull(overrideManager);

        _filePath = filePath;
        _reportItems = reportItems;
        _overrideManager = overrideManager;
        _customColumns = new HashSet<string>(_overrideManager.GetCustomColumns());
    }

    public void WriteReportItemsCsv()
    {
        using var writer = new StreamWriter(File.Create(_filePath));
        WriteReportItemsHeader(writer);
        WriteReportItemsData(writer);
    }

    public void WriteRelationshipsCsv(string filePath)
    {
        using var writer = new StreamWriter(File.Create(filePath));
        WriteRelationshipsHeader(writer);
        WriteRelationshipsData(writer);
    }

    private void WriteReportItemsHeader(StreamWriter writer)
    {
        var headerLine = new StringBuilder("Id,ParentId,StartTime,EndTime,Report,ClassName,MethodName,FullName,Duration,ThreadId,ParentThreadId,NestingLevel");

        foreach (var column in _customColumns)
        {
            headerLine.Append($",{column}");
        }

        var parameterNames = _reportItems.SelectMany(r => r.Parameters.Keys)
                                         .Distinct()
                                         .Where(k => !_customColumns.Contains(k));
        foreach (var parameterName in parameterNames)
        {
            headerLine.Append($",{parameterName}");
        }

        writer.WriteLine(headerLine);
    }

    private void WriteReportItemsData(StreamWriter writer)
    {
        var sortedReportItems = _reportItems
            .Where( item => item.StartTime is not null)
            .OrderBy(item => DateTime.ParseExact(item.StartTime!, "yyyy-MM-dd HH:mm:ss.fff", null))
            .ThenBy(item => item.ThreadId)
            .ThenBy(item => item.Level);

        foreach (var reportItem in sortedReportItems)
        {
            WriteReportItem(writer, reportItem);
        }
    }

    private void WriteReportItem(StreamWriter writer, ReportItem reportItem)
    {
        var parentId = reportItem.Parent ?? "ROOT";
        var fullName = reportItem.FullName ?? string.Empty;
        var overrides = _overrideManager.GetOverridesForMethod(fullName);
        var parameters = new Dictionary<string, string>(reportItem.Parameters);

        foreach (var kvp in overrides)
        {
            parameters[kvp.Key] = kvp.Value;
        }

        reportItem.Parameters = parameters;

        var line = new StringBuilder();
        line.Append($"{reportItem.Id},{parentId},{reportItem.StartTime},{reportItem.EndTime},{reportItem.Report},")
            .Append($"{reportItem.ClassName},{reportItem.MethodName},\"{fullName}\",\"{reportItem.Duration}\",")
            .Append($"{reportItem.ThreadId},{reportItem.ParentThreadId},{reportItem.Level}");

        foreach (var column in _customColumns)
        {
            var value = parameters.GetValueOrDefault(column, "Blank");
            line.Append($",{ReplaceCommas(value)}");
        }

        foreach (var parameter in parameters.Where(p => !_customColumns.Contains(p.Key)))
        {
            line.Append($",\"{ReplaceCommas(parameter.Value)}\"");
        }

        writer.WriteLine(line);
    }

    private void WriteRelationshipsHeader(StreamWriter writer)
    {
        writer.WriteLine("From,To");
    }

    private void WriteRelationshipsData(StreamWriter writer)
    {
        foreach (var item in _reportItems.Where(r => !string.IsNullOrEmpty(r.Parent)))
        {
            writer.WriteLine($"{item.Parent},{item.Id}");
        }
    }

    private static string ReplaceCommas(string value) => value.Replace(",", ";");
}

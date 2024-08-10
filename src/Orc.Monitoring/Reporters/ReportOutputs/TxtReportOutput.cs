namespace Orc.Monitoring.Reporters.ReportOutputs;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MethodLifeCycleItems;
using Monitoring;
using Reporters;

public sealed class TxtReportOutput : IReportOutput
{
    private readonly ReportOutputHelper _helper = new();
    private readonly StringBuilder _buffer = new();
    private readonly List<int> _nestingLevels = [];

    private string? _fileName;
    private string? _folderPath;
    private string? _displayNameParameter;

    public static TxtReportParameters CreateParameters(string folderPath, string displayNameParameter) => new(folderPath, displayNameParameter);

    public IAsyncDisposable Initialize(IMethodCallReporter reporter)
    {
        _helper.Initialize(reporter);

        return new AsyncDisposable(async () =>
        {
            if (_folderPath is null)
            {
                throw new InvalidOperationException("Folder path is not set");
            }

            if (!Directory.Exists(_folderPath))
            {
                Directory.CreateDirectory(_folderPath);
            }

            var rootDisplayName = GetRootDisplayName();
            rootDisplayName = rootDisplayName.Replace(" ", "_");

            _fileName = Path.Combine(_folderPath, $"{reporter.Name}_{rootDisplayName}.txt");

            await File.WriteAllTextAsync(_fileName, _buffer.ToString());

            ReportArchiver.CreateTimestampedFileCopy(_fileName);
            _buffer.Clear();
        });
    }

    public void SetParameters(object? parameters)
    {
        if (parameters is null)
        {
            return;
        }

        var txtParameters = (TxtReportParameters)parameters;
        _folderPath = txtParameters.FolderPath;
        _displayNameParameter = txtParameters.DisplayNameParameter;
    }

    public void WriteSummary(string message)
    {
        _buffer.AppendLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}");
    }

    public void WriteItem(ICallStackItem callStackItem, string? message = null)
    {
        _helper.ProcessCallStackItem(callStackItem);

        switch (callStackItem)
        {
            case IMethodLifeCycleItem methodLifeCycleItem:
                ProcessMethodLifeCycleItem(callStackItem, message, methodLifeCycleItem);
                break;

            case CallGap gap:
                ProcessGap(gap);
                break;

            default:
                _buffer.AppendLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message ?? callStackItem.ToString()}");
                break;
        }
    }

    private void ProcessGap(CallGap gap)
    {
        var endTimestamp = gap.TimeStamp + gap.Elapsed;
        _buffer.AppendLine($"{gap.TimeStamp:yyyy-MM-dd HH:mm:ss.fff} - {endTimestamp:yyyy-MM-dd HH:mm:ss.fff} Gap: {gap.Elapsed.TotalMilliseconds:F2} ms");

        // Add gap parameters
        if (gap.Parameters.Count > 0)
        {
            _buffer.AppendLine("  Gap Parameters:");
            foreach (var parameter in gap.Parameters)
            {
                _buffer.AppendLine($"    {parameter.Key}: {parameter.Value}");
            }
        }
    }

    private void ProcessMethodLifeCycleItem(ICallStackItem callStackItem, string? message,
        IMethodLifeCycleItem methodLifeCycleItem)
    {
        var timestamp = methodLifeCycleItem.TimeStamp;
        var nestingLevel = methodLifeCycleItem.MethodCallInfo.Level;
        var nestingIndex = _nestingLevels.IndexOf(nestingLevel);
        if (nestingIndex == -1)
        {
            _nestingLevels.Add(nestingLevel);
            nestingIndex = _nestingLevels.Count - 1;
        }
        else
        {
            _nestingLevels.RemoveRange(nestingIndex + 1, _nestingLevels.Count - nestingIndex - 1);
        }

        var indentation = string.Join(string.Empty, Enumerable.Repeat("  ", nestingIndex));
        _buffer.AppendLine($"{timestamp:yyyy-MM-dd HH:mm:ss.fff} {indentation}{message ?? callStackItem.ToString()}");

        if (methodLifeCycleItem is MethodCallEnd endItem)
        {
            ProcessMethodCallEnd(endItem, timestamp, indentation);
        }
    }

    private void ProcessMethodCallEnd(MethodCallEnd endItem, DateTime timestamp, string indentation)
    {
        // Add parameters
        var parameters = endItem.MethodCallInfo.Parameters ?? [];
        if (parameters.Count > 0)
        {
            _buffer.AppendLine($"{timestamp:yyyy-MM-dd HH:mm:ss.fff} {indentation}  Parameters:");
            foreach (var parameter in parameters)
            {
                _buffer.AppendLine($"{timestamp:yyyy-MM-dd HH:mm:ss.fff} {indentation}    {parameter.Key}: {parameter.Value}");
            }
        }

        // Add method duration for MethodCallEnd
        _buffer.AppendLine($"{timestamp} {indentation}  Duration: {endItem.MethodCallInfo.Elapsed.TotalMilliseconds:F2} ms");
    }

    public void WriteError(Exception exception)
    {
        var timestamp = DateTime.Now;
        _buffer.AppendLine($"{timestamp:yyyy-MM-dd HH:mm:ss.fff} Error: {exception.Message}");
        _buffer.AppendLine($"{timestamp:yyyy-MM-dd HH:mm:ss.fff} Stack Trace: {exception.StackTrace}");
    }

    private string GetRootDisplayName()
    {
        var rootMethod = _helper.Reporter?.RootMethod;
        if (rootMethod is null)
        {
            return string.Empty;
        }

        return rootMethod.GetCustomAttributes(typeof(MethodCallParameterAttribute), false)
            .OfType<MethodCallParameterAttribute>()
            .FirstOrDefault(x => string.Equals(x.Name, _displayNameParameter, StringComparison.Ordinal))
            ?.Value ?? string.Empty;
    }
}

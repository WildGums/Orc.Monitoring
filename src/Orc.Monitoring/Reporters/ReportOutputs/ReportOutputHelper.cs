namespace Orc.Monitoring.Reporters.ReportOutputs;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Core.Abstractions;
using Core.Factories;
using Core.MethodLifecycle;
using Core.Models;
using Reporters;
using Microsoft.Extensions.Logging;

public class ReportOutputHelper(IMonitoringLoggerFactory loggerFactory, IReportItemFactory reportItemFactory)
{
    private readonly IReportItemFactory _reportItemFactory = reportItemFactory;
    private readonly ILogger<ReportOutputHelper> _logger = loggerFactory.CreateLogger<ReportOutputHelper>();

    private IMethodCallReporter? _reporter;
    private OutputLimitOptions _limitOptions = OutputLimitOptions.Unlimited;
    private Dictionary<string, ReportItem> _reportItems = [];

    public List<ReportItem> Gaps { get; } = [];
    public HashSet<string> ParameterNames { get; } = [];
    public string? LastEndTime { get; private set; }
    public IReadOnlyCollection<ReportItem> ReportItems => _reportItems.Values;
    public IMethodCallReporter? Reporter => _reporter;

    public void Initialize(IMethodCallReporter reporter)
    {
        _reporter = reporter;
        _reportItems.Clear();
        Gaps.Clear();
        ParameterNames.Clear();
        LastEndTime = null;

        _logger.LogInformation($"ReportOutputHelper initialized for reporter: {reporter.GetType().Name}");
    }

    public ReportItem? ProcessCallStackItem(ICallStackItem callStackItem)
    {
        _logger.LogInformation($"ProcessCallStackItem called with {callStackItem.GetType().Name}");

        ReportItem? result = null;
        switch (callStackItem)
        {
            case MethodCallStart start:
                result = ProcessStart(start);
                _logger.LogInformation($"Processed MethodCallStart: {start.MethodCallInfo.MethodName}, Id: {start.MethodCallInfo.Id}, Result: {result.MethodName}");
                break;

            case MethodCallEnd end:
                result = ProcessEnd(end);
                _logger.LogInformation($"Processed MethodCallEnd: {end.MethodCallInfo.MethodName}, Id: {end.MethodCallInfo.Id}, Result: {result.MethodName}");
                break;

            case CallGap gap:
                result = ProcessGap(gap);
                _logger.LogInformation($"Processed CallGap: Duration: {gap.Elapsed}");
                break;
        }

        if (result is not null)
        {
            AddOrUpdateReportItem(result);
            _logger.LogInformation($"Processed {callStackItem.GetType().Name}: {result.MethodName}, Id: {result.Id}");
        }
        else
        {
            _logger.LogWarning($"Failed to process {callStackItem.GetType().Name}");
        }

        _logger.LogInformation($"Current report items count: {_reportItems.Count}");
        return result;
    }

    private void AddOrUpdateReportItem(ReportItem item)
    {
        var existingItem = _reportItems.GetValueOrDefault(item.Id);
        if (existingItem is null)
        {
            _reportItems.Add(item.Id, item);
            _logger.LogInformation($"Added new ReportItem: {item.MethodName}, Id: {item.Id}");
        }
        else
        {
            // Update existing item
            existingItem.EndTime = item.EndTime;
            existingItem.Duration = item.Duration;
            existingItem.Parameters = item.Parameters;
            existingItem.AttributeParameters = item.AttributeParameters;
            _logger.LogInformation($"Updated existing ReportItem: {item.MethodName}, Id: {item.Id}");
        }
    }

    private ReportItem ProcessStart(MethodCallStart start)
    {
        var reporter = Reporter;

        var reportItem = _reportItemFactory.CreateReportItem(start, reporter);

        _logger.LogInformation($"Created new ReportItem: Id={reportItem.Id}, MethodName={reportItem.MethodName}, Parent={reportItem.Parent}");

        return reportItem;
    }

    private ReportItem ProcessEnd(MethodCallEnd end)
    {
        var reportItem = _reportItemFactory.UpdateReportItemEnding(end, Reporter, _reportItems);

        LastEndTime = reportItem.EndTime;
        ParameterNames.UnionWith(reportItem.Parameters.Keys);

        _logger.LogDebug($"Updated report item: {reportItem.MethodName}, Id: {reportItem.Id}, StartTime: {reportItem.StartTime}, EndTime: {reportItem.EndTime}, Duration: {reportItem.Duration}ms");

        return reportItem;
    }

    private ReportItem ProcessGap(CallGap gap)
    {
        var reportItem = _reportItemFactory.CreateGapReportItem(gap, Reporter);

        ParameterNames.UnionWith(reportItem.Parameters.Keys);
        Gaps.Add(reportItem);

        _logger.LogInformation($"Processed gap: Duration: {reportItem.Duration}ms");

        return reportItem;
    }


    public void AddReportItem(ReportItem item)
    {
        var existingItem = _reportItems.GetValueOrDefault(item.Id);
        if (existingItem is null)
        {
            // Add new item
            _reportItems.Add(item.Id, item);
            _logger.LogDebug($"Added new ReportItem: Id={item.Id}, MethodName={item.MethodName}");
        }
        else
        {
            // Update existing item
            existingItem.EndTime = item.EndTime;
            existingItem.Duration = item.Duration;
            _logger.LogDebug($"Updated existing ReportItem: Id={item.Id}, MethodName={item.MethodName}");
        }

        _logger.LogDebug($"Added report item. Current count: {_reportItems.Count}");
        ApplyLimits();
    }

    private void ApplyLimits()
    {
        _logger.LogInformation($"Applying limits. Current ReportItems count: {_reportItems.Count}, Gaps count: {Gaps.Count}");

        if (_limitOptions.MaxItems.HasValue)
        {
            var itemsToRemove = _reportItems.Count - _limitOptions.MaxItems.Value;
            if (itemsToRemove > 0)
            {
                var itemsToKeep = _reportItems.Values
                    .OrderByDescending(i => DateTime.Parse(i.StartTime ?? DateTime.MinValue.ToString(CultureInfo.InvariantCulture)))
                    .Take(_limitOptions.MaxItems.Value)
                    .ToList();

                _reportItems.Clear();
                _reportItems = itemsToKeep.ToDictionary(x => x.Id);

                _logger.LogInformation($"Applied limits. Removed {itemsToRemove} items.");
            }
        }

        _logger.LogInformation($"After applying limits - ReportItems count: {_reportItems.Count}, Gaps count: {Gaps.Count}");
    }

    public void SetLimitOptions(OutputLimitOptions options)
    {
        _limitOptions = options;
        ApplyLimits();
        _logger.LogInformation($"Limit options set: MaxItems = {options.MaxItems}");
    }

    public OutputLimitOptions GetLimitOptions()
    {
        return _limitOptions;
    }

    public string GetDebugInfo()
    {
        return $"ReportItems: {_reportItems.Count}, " +
               $"Gaps: {Gaps.Count}, " +
               $"ParameterNames: {string.Join(", ", ParameterNames)}, " +
               $"LastEndTime: {LastEndTime}, " +
               $"LimitOptions: MaxItems={_limitOptions.MaxItems}";
    }
}

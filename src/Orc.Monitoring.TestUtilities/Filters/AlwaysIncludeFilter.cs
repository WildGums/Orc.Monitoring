namespace Orc.Monitoring.TestUtilities.Filters;

using System;
using Core.Abstractions;
using Core.Models;
using Microsoft.Extensions.Logging;
using Orc.Monitoring.Filters;

public class AlwaysIncludeFilter : IMethodFilter
{
    private readonly ILogger<AlwaysIncludeFilter> _logger;

    public AlwaysIncludeFilter(IMonitoringLoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _logger = loggerFactory.CreateLogger<AlwaysIncludeFilter>();
    }

    public bool ShouldInclude(MethodCallInfo methodCallInfo)
    {
        _logger.LogDebug($"AlwaysIncludeFilter.ShouldInclude(MethodCallInfo) called for {methodCallInfo.MethodName}");
        return true;
    }
}

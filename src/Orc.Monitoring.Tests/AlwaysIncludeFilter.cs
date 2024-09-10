namespace Orc.Monitoring.Tests;

using System;
using Filters;
using Microsoft.Extensions.Logging;

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

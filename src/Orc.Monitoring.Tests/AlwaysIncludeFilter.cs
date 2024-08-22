namespace Orc.Monitoring.Tests;

using System.Reflection;
using Filters;
using Microsoft.Extensions.Logging;

public class AlwaysIncludeFilter : IMethodFilter
{
    private readonly ILogger<AlwaysIncludeFilter> _logger = MonitoringController.CreateLogger<AlwaysIncludeFilter>();

    public bool ShouldInclude(MethodCallInfo methodCallInfo)
    {
        _logger.LogDebug($"AlwaysIncludeFilter.ShouldInclude(MethodCallInfo) called for {methodCallInfo.MethodName}");
        return true;
    }
}

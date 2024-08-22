namespace Orc.Monitoring.Filters;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Reporters;

public class WorkflowItemLevelFilter : IMethodFilter
{
    private readonly int _level;

    private readonly List<string> _sortedLevers = new List<string>()
    {
        MethodCallParameter.Levels.Low,
        MethodCallParameter.Levels.Medium,
        MethodCallParameter.Levels.High,
    };

    public WorkflowItemLevelFilter(string level)
    {
        _level = _sortedLevers.IndexOf(level);
    }

    public bool ShouldInclude(MethodCallInfo methodCallInfo)
    {
        var level = methodCallInfo.Parameters?.GetValueOrDefault(MethodCallParameter.WorkflowItemLevel);
        return ShouldIncludeInternal(level);
    }

    private bool ShouldIncludeInternal(string? level)
    {
        if(level is null)
        {
            return true;
        }

        var methodLevel = _sortedLevers.IndexOf(level);

        return methodLevel >= _level;
    }
}

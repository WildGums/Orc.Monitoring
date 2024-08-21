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

    public bool ShouldInclude(MethodInfo methodInfo)
    {
        return methodInfo.GetCustomAttributes<MethodCallParameterAttribute>()
            .Any(x => string.Equals(x.Name, MethodCallParameter.WorkflowItemLevel, StringComparison.Ordinal) && ShouldIncludeInternal(x.Value));
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

namespace Orc.Monitoring.Filters;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Reporters;

public class WorkflowItemLevelFilter : IMethodFilter
{
    private readonly HashSet<string> _levels;

    public WorkflowItemLevelFilter(params string[] levels)
    {
        _levels = levels.ToHashSet();
    }

    public bool ShouldInclude(MethodInfo methodInfo)
    {
        return methodInfo.GetCustomAttributes<MethodCallParameterAttribute>()
            .Any(x => string.Equals(x.Name, MethodCallParameter.WorkflowItemLevel, StringComparison.Ordinal) && _levels.Contains(x.Value));
    }

    public bool ShouldInclude(MethodCallInfo methodCallInfo) => methodCallInfo.Parameters?.TryGetValue(MethodCallParameter.WorkflowItemLevel, out var level) == true && _levels.Contains(level);
}

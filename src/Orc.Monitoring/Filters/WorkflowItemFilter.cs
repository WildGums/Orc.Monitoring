namespace Orc.Monitoring.Filters;

using System;
using System.Linq;
using Monitoring;
using Reporters;

public class WorkflowItemFilter : IMethodFilter
{
    public bool ShouldInclude(MethodCallInfo methodCallInfo)
    {
        if (methodCallInfo.Parameters is null)
        {
            return false;
        }

        return methodCallInfo.Parameters
            .Any(p => string.Equals(p.Key, MethodCallParameter.WorkflowItemName, StringComparison.OrdinalIgnoreCase)
                      && !string.IsNullOrWhiteSpace(p.Value));
    }
}

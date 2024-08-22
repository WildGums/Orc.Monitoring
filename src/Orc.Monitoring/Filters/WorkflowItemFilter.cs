namespace Orc.Monitoring.Filters;

using System;
using System.Linq;
using System.Reflection;
using Monitoring;
using Reporters;

public class WorkflowItemFilter : IMethodFilter
{
    public bool ShouldInclude(MethodCallInfo methodCallInfo) => methodCallInfo.Parameters?.ContainsKey(MethodCallParameter.WorkflowItemName) ?? false;
}

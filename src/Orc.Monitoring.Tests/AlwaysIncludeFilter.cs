namespace Orc.Monitoring.Tests;

using System.Reflection;
using Filters;

public class AlwaysIncludeFilter : IMethodFilter
{
    public bool ShouldInclude(MethodInfo methodInfo) => true;
    public bool ShouldInclude(MethodCallInfo methodCallInfo) => true;
}

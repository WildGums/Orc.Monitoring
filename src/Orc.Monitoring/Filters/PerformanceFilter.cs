namespace Orc.Monitoring.Filters;

using System.Reflection;


public class PerformanceFilter : IMethodFilter
{
    public bool ShouldInclude(MethodInfo methodInfo)
    {
        // In a real implementation, we might check for specific attributes or method characteristics
        // For testing purposes, we'll include all methods
        return true;
    }

    public bool ShouldInclude(MethodCallInfo methodCallInfo)
    {
        // For testing purposes, we'll include all method calls
        return true;
    }
}

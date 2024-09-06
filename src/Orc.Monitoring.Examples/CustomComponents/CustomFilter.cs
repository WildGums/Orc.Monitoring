namespace Orc.Monitoring.Examples.CustomComponents;

using Orc.Monitoring;
using Orc.Monitoring.Filters;

public class CustomFilter : IMethodFilter
{
    public bool ShouldInclude(MethodCallInfo methodCallInfo)
    {
        // Example: Include only methods that have a duration longer than 100ms
        if (methodCallInfo.Elapsed.TotalMilliseconds > 100)
        {
            return true;
        }

        // Example: Always include methods with specific names
        if (methodCallInfo?.MethodName?.Contains("Important") ?? false)
        {
            return true;
        }

        return false;
    }
}

namespace Orc.Monitoring.Filters;

using System.Reflection;
using Orc.Monitoring;

public interface IMethodFilter
{
    bool ShouldInclude(MethodInfo methodInfo);
    bool ShouldInclude(MethodCallInfo methodCallInfo);
}

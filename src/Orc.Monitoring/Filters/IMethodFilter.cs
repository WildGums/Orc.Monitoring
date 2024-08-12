// ReSharper disable InconsistentNaming
namespace Orc.Monitoring.Filters;

using System.Reflection;
using Monitoring;

public interface IMethodFilter
{
    bool ShouldInclude(MethodInfo methodInfo);
    bool ShouldInclude(MethodCallInfo methodCallInfo);
}

// ReSharper disable InconsistentNaming
namespace Orc.Monitoring.Filters;

using Monitoring;

public interface IMethodFilter
{
    bool ShouldInclude(MethodCallInfo methodCallInfo);
}

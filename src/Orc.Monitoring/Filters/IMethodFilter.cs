// ReSharper disable InconsistentNaming
namespace Orc.Monitoring.Filters;

using System.Reflection;
using Monitoring;

public interface IMethodFilter
{
    bool ShouldInclude(MethodCallInfo methodCallInfo);
}

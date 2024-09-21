// ReSharper disable InconsistentNaming
namespace Orc.Monitoring.Core.Abstractions;

using Models;

public interface IMethodFilter
{
    bool ShouldInclude(MethodCallInfo methodCallInfo);
}

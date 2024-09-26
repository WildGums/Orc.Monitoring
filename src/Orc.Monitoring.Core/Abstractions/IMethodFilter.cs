// ReSharper disable InconsistentNaming
namespace Orc.Monitoring.Core.Abstractions;

using Models;

public interface IMethodFilter : IMonitoringComponent
{
    bool ShouldInclude(MethodCallInfo methodCallInfo);
}

public interface IMonitoringComponent
{
    // Marker interface, can be extended with common properties if needed
}

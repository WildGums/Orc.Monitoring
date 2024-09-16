// ReSharper disable InconsistentNaming
namespace Orc.Monitoring;

using System.Runtime.CompilerServices;
using MethodLifeCycleItems;

public interface IClassMonitor
{
    IMethodCallContext StartAsyncMethod(MethodConfiguration config, [CallerMemberName] string callerMethod = "");
    IMethodCallContext StartMethod(MethodConfiguration config, [CallerMemberName] string callerMethod = "");
    void LogStatus(IMethodLifeCycleItem status);
}

// ReSharper disable InconsistentNaming
namespace Orc.Monitoring;

using System.Runtime.CompilerServices;
using MethodLifeCycleItems;

public interface IClassMonitor
{
    AsyncMethodCallContext StartAsyncMethod(MethodConfiguration config, [CallerMemberName] string callerMethod = "");
    MethodCallContext StartMethod(MethodConfiguration config, [CallerMemberName] string callerMethod = "");
    void LogStatus(IMethodLifeCycleItem status);
}

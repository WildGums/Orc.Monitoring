// ReSharper disable InconsistentNaming
namespace Orc.Monitoring.Core.Abstractions;

using System;
using System.Runtime.CompilerServices;
using Configuration;
using MethodLifecycle;

public interface IClassMonitor
{
    IMethodCallContext StartAsyncMethod(MethodConfiguration config, [CallerMemberName] string callerMethod = "");
    IMethodCallContext StartMethod(MethodConfiguration config, [CallerMemberName] string callerMethod = "");

    IMethodCallContext StartExternalMethod(MethodConfiguration config, Type externalType, string externalMethodName,
        bool async = false);
    void LogStatus(IMethodLifeCycleItem status);
}

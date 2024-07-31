namespace Orc.Monitoring;

using System;
using System.Runtime.CompilerServices;

public static class ClassMonitorExtensions
{
    public static AsyncMethodCallContext AsyncStart(this IClassMonitor monitor, Action<MethodConfigurationBuilder> configAction, [CallerMemberName] string callerMethod = "")
    {
        var builder = new MethodConfigurationBuilder();
        configAction(builder);
        var config = builder.Build();
        return monitor.StartAsyncMethod(config, callerMethod);
    }

    public static MethodCallContext Start(this IClassMonitor monitor, Action<MethodConfigurationBuilder> configAction, [CallerMemberName] string callerMethod = "")
    {
        var builder = new MethodConfigurationBuilder();
        configAction(builder);
        var config = builder.Build();
        return monitor.StartMethod(config, callerMethod);
    }
}

﻿namespace Orc.Monitoring;

using System;
using System.Runtime.CompilerServices;

public static class ClassMonitorExtensions
{
    public static IMethodCallContext AsyncStart(this IClassMonitor monitor, Action<MethodConfigurationBuilder> configAction, [CallerMemberName] string callerMethod = "")
    {
        var builder = new MethodConfigurationBuilder();
        configAction(builder);
        var config = builder.Build();
        return monitor.StartAsyncMethod(config, callerMethod);
    }

    public static IMethodCallContext Start(this IClassMonitor monitor, Action<MethodConfigurationBuilder> configAction, [CallerMemberName] string callerMethod = "")
    {
        var builder = new MethodConfigurationBuilder();
        configAction(builder);
        var config = builder.Build();
        return monitor.StartMethod(config, callerMethod);
    }

    public static IMethodCallContext StartExternal(this IClassMonitor monitor, Action<MethodConfigurationBuilder> configAction, Type externalType, [CallerMemberName] string callerMethod = "")
    {
        var builder = new MethodConfigurationBuilder();
        configAction(builder);
        var config = builder.Build();
        return monitor.StartExternalMethod(config, externalType, callerMethod, false);
    }

    public static IMethodCallContext AsyncStartExternal(this IClassMonitor monitor, Action<MethodConfigurationBuilder> configAction, Type externalType, [CallerMemberName] string callerMethod = "")
    {
        var builder = new MethodConfigurationBuilder();
        configAction(builder);
        var config = builder.Build();
        return monitor.StartExternalMethod(config, externalType, callerMethod, true);
    }
}

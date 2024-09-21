﻿namespace Orc.Monitoring.Core.MethodCallContexts;

using System;
using System.Collections.Generic;
using Abstractions;
using Controllers;
using Models;
using Monitoring.Utilities.Logging;
using Pooling;

public class MethodCallContextFactory(IMonitoringController monitoringController, IMonitoringLoggerFactory loggerFactory, MethodCallInfoPool methodCallInfoPool) : IMethodCallContextFactory
{
    internal static MethodCallContextFactory Instance { get; } = new(MonitoringController.Instance, MonitoringLoggerFactory.Instance, MethodCallInfoPool.Instance);

    public IMethodCallContext GetDummyMethodCallContext()
    {
        return NullMethodCallContext.Instance;
    }

    public IMethodCallContext CreateMethodCallContext(IClassMonitor? classMonitor, MethodCallInfo methodCallInfo, List<IAsyncDisposable> disposables, IEnumerable<string> reporterIds)
    {
#pragma warning disable IDISP005
        return new MethodCallContext(classMonitor, methodCallInfo, disposables, reporterIds, loggerFactory, monitoringController, methodCallInfoPool);
#pragma warning restore IDISP005
    }

    public IMethodCallContext GetDummyAsyncMethodCallContext()
    {
        return NullMethodCallContext.Instance;
    }

    public IMethodCallContext CreateAsyncMethodCallContext(IClassMonitor? classMonitor, MethodCallInfo methodCallInfo, List<IAsyncDisposable> disposables, IEnumerable<string> reporterIds)
    {
#pragma warning disable IDISP005
        return new AsyncMethodCallContext(classMonitor, methodCallInfo, disposables, reporterIds, loggerFactory, monitoringController, methodCallInfoPool);
#pragma warning restore IDISP005
    }
}

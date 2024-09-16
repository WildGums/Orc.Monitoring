﻿namespace Orc.Monitoring;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MethodLifeCycleItems;
using Microsoft.Extensions.Logging;

public abstract class MethodCallContextBase : VersionedMonitoringContext, IMethodCallContext
{
    protected readonly IMonitoringController _monitoringController;
    protected readonly MethodCallInfoPool _methodCallInfoPool;
    protected readonly ILogger _logger;
    protected readonly IClassMonitor? _classMonitor;
    protected readonly IReadOnlyCollection<IAsyncDisposable>? _disposables;
    protected readonly System.Diagnostics.Stopwatch _stopwatch = new();
    protected bool _isDisposed;

    public MethodCallInfo? MethodCallInfo { get; protected set; }
    public IReadOnlyList<string> ReporterIds { get; protected set; } = Array.Empty<string>();

    protected MethodCallContextBase(
        IClassMonitor? classMonitor,
        MethodCallInfo? methodCallInfo,
        List<IAsyncDisposable>? disposables,
        IEnumerable<string>? reporterIds,
        ILogger logger,
        IMonitoringController monitoringController,
        MethodCallInfoPool methodCallInfoPool)
        : base(monitoringController)
    {
        _classMonitor = classMonitor;
        MethodCallInfo = methodCallInfo;
        _disposables = disposables;
        ReporterIds = reporterIds?.ToArray() ?? [];
        _logger = logger;
        _monitoringController = monitoringController;
        _methodCallInfoPool = methodCallInfoPool;

        if (MethodCallInfo is not null && !MethodCallInfo.IsNull)
        {
            _stopwatch.Start();
            var startStatus = new MethodCallStart(MethodCallInfo);
            (_classMonitor as ClassMonitor)?.LogStatus(startStatus);
        }
    }

    public abstract void LogException(Exception exception);
    public abstract void Log(string category, object data);
    public abstract void SetParameter(string name, string value);

    public abstract void Dispose();
    public abstract ValueTask DisposeAsync();
}

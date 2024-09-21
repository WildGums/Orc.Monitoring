namespace Orc.Monitoring.Core.Abstractions;

using System;
using System.Collections.Generic;
using Models;

public interface IMethodCallContextFactory
{
    IMethodCallContext GetDummyMethodCallContext();

    IMethodCallContext CreateMethodCallContext(IClassMonitor? classMonitor, MethodCallInfo methodCallInfo, List<IAsyncDisposable> disposables, IEnumerable<string> reporterIds);

    IMethodCallContext GetDummyAsyncMethodCallContext();

    IMethodCallContext CreateAsyncMethodCallContext(IClassMonitor? classMonitor, MethodCallInfo methodCallInfo, List<IAsyncDisposable> disposables, IEnumerable<string> reporterIds);
}

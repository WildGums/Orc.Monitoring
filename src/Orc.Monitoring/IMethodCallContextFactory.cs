namespace Orc.Monitoring;

using System;
using System.Collections.Generic;

public interface IMethodCallContextFactory
{
    MethodCallContext GetDummyMethodCallContext();

    MethodCallContext CreateMethodCallContext(IClassMonitor? classMonitor, MethodCallInfo methodCallInfo, List<IAsyncDisposable> disposables, IEnumerable<string> reporterIds);

    AsyncMethodCallContext GetDummyAsyncMethodCallContext();

    AsyncMethodCallContext CreateAsyncMethodCallContext(IClassMonitor? classMonitor, MethodCallInfo methodCallInfo, List<IAsyncDisposable> disposables, IEnumerable<string> reporterIds);
}

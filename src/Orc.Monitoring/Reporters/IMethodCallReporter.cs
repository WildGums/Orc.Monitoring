// ReSharper disable InconsistentNaming
namespace Orc.Monitoring.Reporters;

using System;
using System.Collections.Generic;
using System.Reflection;
using MethodLifeCycleItems;
using Orc.Monitoring.Filters;

/// <summary>
/// Defines a method for reporting performance data.
/// </summary>
public interface IMethodCallReporter : IOutputContainer
{
    string Name { get; }
    string FullName { get; }
    MethodInfo? RootMethod { get; set; }

    IAsyncDisposable StartReporting(IObservable<ICallStackItem> callStack);

    void AddFilter(IMethodFilter filter);
    void RemoveFilter(IMethodFilter filter);
    IReadOnlyList<IMethodFilter> GetFilters();
}

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
    /// <summary>
    /// Gets the name of the reporter.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the full name of the reporter.
    /// </summary>
    string FullName { get; }

    /// <summary>
    /// Gets the root method for this reporter.
    /// </summary>
    MethodInfo RootMethod { get; }

    /// <summary>
    /// Gets or sets the unique identifier for this reporter instance.
    /// </summary>
    string Id { get; set; }

    /// <summary>
    /// Starts reporting on the provided call stack.
    /// </summary>
    /// <param name="callStack">The observable call stack to report on.</param>
    /// <returns>An IAsyncDisposable that can be used to stop reporting.</returns>
    IAsyncDisposable StartReporting(IObservable<ICallStackItem> callStack);

    void Initialize(MonitoringConfiguration monitoringConfiguration, MethodCallInfo rootMethod);
    void SetRootMethod(MethodInfo methodInfo);
}

// ReSharper disable InconsistentNaming
namespace Orc.Monitoring.Core.Abstractions;

using System;
using System.Reflection;
using Configuration;
using MethodLifecycle;
using Models;

/// <summary>
/// Defines a method for reporting performance data.
/// </summary>
public interface IMethodCallReporter : IOutputContainer, IMonitoringComponent
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
    MethodInfo? RootMethod { get; }

    /// <summary>
    /// Starts reporting on the provided call stack.
    /// </summary>
    /// <param name="callStack">The observable call stack to report on.</param>
    /// <returns>An IAsyncDisposable that can be used to stop reporting.</returns>
    IAsyncDisposable StartReporting(IObservable<ICallStackItem> callStack);

    void Initialize(MonitoringConfiguration monitoringConfiguration, MethodCallInfo rootMethod);
}

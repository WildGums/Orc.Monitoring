// ReSharper disable InconsistentNaming
namespace Orc.Monitoring.Core.Abstractions;

using System;
using MethodLifecycle;

public interface IReportOutput : IMonitoringComponent
{
    IAsyncDisposable Initialize(IMethodCallReporter reporter);
    IReportOutput SetParameters(object? parameter = null);
    void WriteSummary(string message);
    void WriteItem(ICallStackItem callStackItem, string? message = null);
    void WriteError(Exception exception);
}

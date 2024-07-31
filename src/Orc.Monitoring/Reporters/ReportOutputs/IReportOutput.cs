﻿namespace Orc.Monitoring.Reporters.ReportOutputs;

using System;
using MethodLifeCycleItems;
using Orc.Monitoring.Reporters;

public interface IReportOutput
{
    IAsyncDisposable Initialize(IMethodCallReporter reporter);
    void SetParameters(object? parameter = null);
    void WriteSummary(string message);
    void WriteItem(ICallStackItem callStackItem, string? message = null);
    void WriteError(Exception exception);
}

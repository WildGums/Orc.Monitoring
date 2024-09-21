namespace Orc.Monitoring.Core.MethodCallContexts;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Abstractions;
using Models;

public sealed class NullMethodCallContext : IMethodCallContext
{
    public static NullMethodCallContext Instance { get; } = new NullMethodCallContext();

    private NullMethodCallContext() { }

    public MethodCallInfo? MethodCallInfo => null;
    public IReadOnlyList<string> ReporterIds => Array.Empty<string>();

    public void LogException(Exception exception) { /* No-op */ }
    public void Log<T>(string category, T data) { /* No-op */ }
    public void SetParameter(string name, string value) { /* No-op */ }

    public void Dispose() { /* No-op */ }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

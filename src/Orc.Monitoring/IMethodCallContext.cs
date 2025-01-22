namespace Orc.Monitoring;

using System;
using System.Collections.Generic;

public interface IMethodCallContext : IDisposable, IAsyncDisposable
{
    MethodCallInfo? MethodCallInfo { get; }
    IReadOnlyList<string> ReporterIds { get; }
    void LogException(Exception exception);
    void Log<T>(string category, T data);
    void SetParameter(string name, string value);
}

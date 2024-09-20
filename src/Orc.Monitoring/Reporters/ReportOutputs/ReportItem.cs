namespace Orc.Monitoring.Reporters.ReportOutputs;

using System.Collections.Generic;

public class ReportItem
{
    public required string Id { get; set; }
    public string? StartTime { get; set; }
    public string? ItemName { get; set; }
    public string? EndTime { get; set; }
    public string? Duration { get; set; }
    public string? Report { get; set; }
    public string? ThreadId { get; set; }
    public string? Level { get; set; }
    public string? ClassName { get; set; }
    public string? MethodName { get; set; }
    public string? FullName { get; set; }
    public string? Parent { get; set; }
    public string? ParentThreadId { get; set; }
    public IReadOnlyDictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();
    public HashSet<string> AttributeParameters { get; set; } = [];
    public bool IsRoot { get; set; }
    public bool IsStaticParameter(string parameterName) => AttributeParameters.Contains(parameterName);
}

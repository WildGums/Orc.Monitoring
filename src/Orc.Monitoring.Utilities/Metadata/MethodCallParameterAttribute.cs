namespace Orc.Monitoring.Utilities.Metadata;

using System;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class MethodCallParameterAttribute(string name, string value) : Attribute
{
    public string Name { get; } = name;
    public string Value { get; } = value;
}

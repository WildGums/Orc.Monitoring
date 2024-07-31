namespace Orc.Monitoring;

using System;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class MethodCallParameterAttribute : Attribute
{
    public MethodCallParameterAttribute(string name, string value)
    {
        Name = name;
        Value = value;
    }

    public string Name { get; }
    public string Value { get; }
}

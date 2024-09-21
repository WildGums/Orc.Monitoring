namespace Orc.Monitoring.Core.Attributes;

using System;

[AttributeUsage(AttributeTargets.Class)]
public class DefaultOutputAttribute : Attribute
{
    public DefaultOutputAttribute(bool isEnabled = true)
    {
        IsEnabled = isEnabled;
    }

    public bool IsEnabled { get; }
}

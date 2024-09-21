namespace Orc.Monitoring.Utilities.Metadata;

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

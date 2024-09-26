namespace Orc.Monitoring.Core.Configuration;

using System;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class DefaultComponentAttribute : Attribute
{
    public bool IsEnabled { get; }

    public DefaultComponentAttribute(bool isEnabled = true)
    {
        IsEnabled = isEnabled;
    }
}

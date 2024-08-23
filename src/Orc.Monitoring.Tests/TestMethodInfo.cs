namespace Orc.Monitoring.Tests;

using System;
using System.Reflection;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;

public class TestMethodInfo : MethodInfo
{
    public override string Name { get; }
    public override Type? DeclaringType { get; }
    private readonly List<Attribute> _customAttributes = new();

    public TestMethodInfo(string name, Type declaringType)
    {
        Name = name;
        DeclaringType = declaringType;
    }

    public void SetCustomAttribute(Attribute attribute)
    {
        _customAttributes.Add(attribute);
    }

    public override object[] GetCustomAttributes(bool inherit)
    {
        return _customAttributes.ToArray();
    }

    public override object[] GetCustomAttributes(Type attributeType, bool inherit)
    {
        return _customAttributes.Where(attr => attributeType.IsInstanceOfType(attr)).ToArray();
    }

    public override bool IsDefined(Type attributeType, bool inherit)
    {
        return _customAttributes.Any(attr => attributeType.IsInstanceOfType(attr));
    }

    // Implement other MethodInfo members with default implementations
    public override MethodAttributes Attributes => MethodAttributes.Public;
    public override RuntimeMethodHandle MethodHandle => throw new NotImplementedException();
    public override Type? ReflectedType => DeclaringType;
    public override ICustomAttributeProvider ReturnTypeCustomAttributes => throw new NotImplementedException();
    public override MethodInfo GetBaseDefinition() => this;
    public override MethodImplAttributes GetMethodImplementationFlags() => MethodImplAttributes.Managed;
    public override ParameterInfo[] GetParameters() => Array.Empty<ParameterInfo>();
    public override object? Invoke(object? obj, BindingFlags invokeAttr, Binder? binder, object?[]? parameters, CultureInfo? culture) => throw new NotImplementedException();
}

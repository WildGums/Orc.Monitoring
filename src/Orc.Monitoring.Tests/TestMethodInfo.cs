namespace Orc.Monitoring.Tests;

using System;
using System.Reflection;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;

public class TestMethodInfo(string name, Type declaringType) : MethodInfo
{
    public override string Name { get; } = name;
    public override Type? DeclaringType { get; } = declaringType;
    private readonly List<Attribute> _customAttributes = [];

    public void SetCustomAttribute(Attribute attribute)
    {
        _customAttributes.Add(attribute);
    }

    public override Attribute[] GetCustomAttributes(bool inherit)
    {
        return _customAttributes.ToArray();
    }

    public override Attribute[] GetCustomAttributes(Type attributeType, bool inherit)
    {
        return _customAttributes.Where(attributeType.IsInstanceOfType).ToArray();
    }

    public override bool IsDefined(Type attributeType, bool inherit)
    {
        return _customAttributes.Any(attributeType.IsInstanceOfType);
    }

    // Implement other MethodInfo members with default implementations
    public override MethodAttributes Attributes => MethodAttributes.Public;
    public override RuntimeMethodHandle MethodHandle => throw new NotImplementedException();
    public override Type? ReflectedType => DeclaringType;
    public override ICustomAttributeProvider ReturnTypeCustomAttributes => throw new NotImplementedException();
    public override MethodInfo GetBaseDefinition() => this;
    public override MethodImplAttributes GetMethodImplementationFlags() => MethodImplAttributes.Managed;
    public override ParameterInfo[] GetParameters() => [];
    public override object? Invoke(object? obj, BindingFlags invokeAttr, Binder? binder, object?[]? parameters, CultureInfo? culture) => throw new NotImplementedException();
}

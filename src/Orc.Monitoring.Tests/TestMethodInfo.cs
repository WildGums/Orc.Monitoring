namespace Orc.Monitoring.Tests;

using System.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;

public class TestMethodInfo : MethodInfo
{
    private readonly Dictionary<Type, List<Attribute>> _customAttributes = new Dictionary<Type, List<Attribute>>();

    public TestMethodInfo(string name, Type declaringType)
    {
        Name = name;
        DeclaringType = declaringType;
    }

    public override string Name { get; }
    public override Type? ReflectedType => DeclaringType;
    public override Type DeclaringType { get; }

    // Implement other MethodInfo members with default implementations
    public override MethodAttributes Attributes => MethodAttributes.Public;
    public override RuntimeMethodHandle MethodHandle => throw new NotImplementedException();
    public override Type ReturnType => typeof(void);
    public override ICustomAttributeProvider ReturnTypeCustomAttributes => throw new NotImplementedException();
    public override MethodInfo GetBaseDefinition() => this;

    public void SetCustomAttribute(Attribute attribute)
    {
        var attributeType = attribute.GetType();
        if (!_customAttributes.TryGetValue(attributeType, out var attributes))
        {
            attributes = [];
            _customAttributes[attributeType] = attributes;
        }
        attributes.Add(attribute);
    }

    public override Attribute[] GetCustomAttributes(bool inherit)
    {
        return _customAttributes.Values.SelectMany(a => a).ToArray();
    }

    public override Attribute[] GetCustomAttributes(Type attributeType, bool inherit)
    {
        if (_customAttributes.TryGetValue(attributeType, out var attributes))
        {
            // Create an array of the specific attribute type
            Array typedArray = Array.CreateInstance(attributeType, attributes.Count);
            for (int i = 0; i < attributes.Count; i++)
            {
                typedArray.SetValue(attributes[i], i);
            }
            // Cast the typed array to Attribute[]
            return (Attribute[])typedArray;
        }
        return [];
    }

    public override MethodImplAttributes GetMethodImplementationFlags() => MethodImplAttributes.Managed;
    public override ParameterInfo[] GetParameters() => [];

    public override object? Invoke(object? obj, BindingFlags invokeAttr, Binder? binder, object?[]? parameters, System.Globalization.CultureInfo? culture)
    {
        throw new NotImplementedException();
    }

    public override bool IsDefined(Type attributeType, bool inherit)
    {
        return _customAttributes.ContainsKey(attributeType);
    }
}

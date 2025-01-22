namespace Orc.Monitoring.Utilities;

using System.Collections.Generic;
using System.Reflection;
using System;
using System.Linq;

public static class ReflectionHelper
{
    public static MethodInfo? FindMatchingMethod(Type? classType, string? methodName, IReadOnlyCollection<Type> genericArguments, IReadOnlyCollection<Type> parameterTypes)
    {
        var bindingFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        if (classType is null)
        {
            return null;
        }

        if (methodName is null)
        {
            return null;
        }

        // First, try to find the method in the current class
        var currentClassMethods = classType.GetMethods(bindingFlags)
            .Where(m => m.Name == methodName)
            .ToList();

        var matchedMethod = FindBestMatch(classType, currentClassMethods, genericArguments, parameterTypes);
        if (matchedMethod is not null)
        {
            return matchedMethod;
        }

        // If not found in the current class, search through the hierarchy
        var allMethods = new List<MethodInfo>();
        var currentType = classType.BaseType;

        while (currentType is not null)
        {
            allMethods.AddRange(currentType.GetMethods(bindingFlags).Where(m => m.Name == methodName));
            currentType = currentType.BaseType;
        }

        matchedMethod = FindBestMatch(classType, currentClassMethods, genericArguments, parameterTypes);
        if (matchedMethod is not null)
        {
            return matchedMethod;
        }

        // If we still can't determine, throw an exception
        var classTypeName = classType?.Name ?? string.Empty;
        throw new InvalidOperationException($"No matching method named {methodName} found in {classTypeName} or its base classes that matches the provided configuration.");
    }

    private static MethodInfo? FindBestMatch(Type classType, List<MethodInfo> methods, IReadOnlyCollection<Type> genericArguments, IReadOnlyCollection<Type> parameterTypes)
    {
        if (!methods.Any())
        {
            return null;
        }

        if (methods.Count == 1)
        {
            return methods[0];
        }


        // If we have multiple methods, try to match based on parameter types
        if (parameterTypes.Any())
        {
            var matchedMethod = methods.FirstOrDefault(m => ParametersMatch(m.GetParameters(), parameterTypes));
            if (matchedMethod is not null)
            {
                return matchedMethod;
            }
        }

        // If we still can't determine, and it's a generic method, try to match based on generic arguments
        if (genericArguments.Any())
        {
            var matchedMethod = methods.FirstOrDefault(m => GenericArgumentsMatch(m, genericArguments));
            if (matchedMethod is not null)
            {
                return matchedMethod;
            }
        }

        var classTypeName = classType.Name ?? string.Empty;

        throw new AmbiguousMatchException(
            $"Multiple methods found in {classTypeName} with the name {methods[0].Name} that match the provided configuration.");
    }

    private static bool ParametersMatch(ParameterInfo[] methodParams, IReadOnlyCollection<Type> configParams)
    {
        if (methodParams.Length != configParams.Count)
        {
            return false;
        }

        for (var i = 0; i < methodParams.Length; i++)
        {
            var methodParameterType = methodParams[i].ParameterType;
            var parameterType = configParams.ElementAt(i);
            if (methodParameterType.IsAssignableFrom(parameterType))
            {
                continue;
            }

            if (!methodParameterType.IsGenericParameter)
            {
                return false;
            }

            if(methodParameterType.BaseType is null)
            {
                return false;
            }

            if (methodParameterType.BaseType.IsAssignableFrom(parameterType))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private static bool GenericArgumentsMatch(MethodInfo method, IReadOnlyCollection<Type> configGenericArgs)
    {
        if (!method.IsGenericMethod)
        {
            return false;
        }

        var genericArgs = method.GetGenericArguments();
        if (genericArgs.Length != configGenericArgs.Count)
        {
            return false;
        }

        // Compare each generic argument type
        var methodGenericArgs = method.GetGenericMethodDefinition().GetGenericArguments();
        for (int i = 0; i < genericArgs.Length; i++)
        {
            if (genericArgs[i].Name != methodGenericArgs[i].Name)
            {
                return false;
            }
        }

        return true;
    }
}

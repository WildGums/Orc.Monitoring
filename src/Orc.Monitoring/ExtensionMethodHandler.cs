namespace Orc.Monitoring;

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

public class ExtensionMethodHandler
{
    private readonly ILogger<ExtensionMethodHandler> _logger;
    private readonly Dictionary<MethodInfo, Type> _extensionMethods;
    private readonly Dictionary<Type, HashSet<MethodInfo>> _extendedTypeToMethods;
    private readonly Dictionary<MethodInfo, int> _invocationCounts;

    public ExtensionMethodHandler()
    {
        _logger = MonitoringController.CreateLogger<ExtensionMethodHandler>();
        _extensionMethods = new Dictionary<MethodInfo, Type>();
        _extendedTypeToMethods = new Dictionary<Type, HashSet<MethodInfo>>();
        _invocationCounts = new Dictionary<MethodInfo, int>();
    }

    public void RegisterExtensionMethod(MethodInfo methodInfo)
    {
        if (!methodInfo.IsDefined(typeof(ExtensionAttribute), false))
        {
            _logger.LogWarning($"Attempted to register non-extension method: {methodInfo.Name}");
            return;
        }

        var parameters = methodInfo.GetParameters();
        if (parameters.Length == 0)
        {
            _logger.LogWarning($"Invalid extension method (no parameters): {methodInfo.Name}");
            return;
        }

        var extendedType = parameters[0].ParameterType;

        lock (_extensionMethods)
        {
            _extensionMethods[methodInfo] = extendedType;

            if (!_extendedTypeToMethods.TryGetValue(extendedType, out var methods))
            {
                methods = new HashSet<MethodInfo>();
                _extendedTypeToMethods[extendedType] = methods;
            }
            methods.Add(methodInfo);

            _logger.LogInformation($"Registered extension method: {methodInfo.Name} for type {extendedType.Name}");
        }
    }

    public void TrackInvocation(MethodInfo methodInfo)
    {
        if (!_extensionMethods.ContainsKey(methodInfo))
        {
            _logger.LogWarning($"Attempted to track invocation of unregistered method: {methodInfo.Name}");
            return;
        }

        lock (_invocationCounts)
        {
            if (_invocationCounts.TryGetValue(methodInfo, out var count))
            {
                _invocationCounts[methodInfo] = count + 1;
            }
            else
            {
                _invocationCounts[methodInfo] = 1;
            }
        }

        _logger.LogDebug($"Tracked invocation of extension method: {methodInfo.Name}");
    }

    public Type? GetExtendedType(MethodInfo methodInfo)
    {
        lock (_extensionMethods)
        {
            return _extensionMethods.TryGetValue(methodInfo, out var type) ? type : null;
        }
    }

    public IEnumerable<MethodInfo> GetExtensionMethodsForType(Type type)
    {
        lock (_extendedTypeToMethods)
        {
            return _extendedTypeToMethods.TryGetValue(type, out var methods) ? methods.ToList() : Enumerable.Empty<MethodInfo>();
        }
    }

    public int GetInvocationCount(MethodInfo methodInfo)
    {
        lock (_invocationCounts)
        {
            return _invocationCounts.TryGetValue(methodInfo, out var count) ? count : 0;
        }
    }

    public IEnumerable<KeyValuePair<MethodInfo, int>> GetAllInvocationCounts()
    {
        lock (_invocationCounts)
        {
            return _invocationCounts.ToList();
        }
    }

    public void Clear()
    {
        lock (_extensionMethods)
        {
            _extensionMethods.Clear();
        }

        lock (_extendedTypeToMethods)
        {
            _extendedTypeToMethods.Clear();
        }

        lock (_invocationCounts)
        {
            _invocationCounts.Clear();
        }

        _logger.LogInformation("Cleared all tracked extension method data");
    }

    public bool IsExtensionMethod(MethodInfo methodInfo)
    {
        lock (_extensionMethods)
        {
            return _extensionMethods.ContainsKey(methodInfo);
        }
    }

    public IEnumerable<MethodInfo> GetAllTrackedExtensionMethods()
    {
        lock (_extensionMethods)
        {
            return _extensionMethods.Keys.ToList();
        }
    }

    public Dictionary<string, object?> GetExtensionMethodMetadata(MethodInfo methodInfo)
    {
        var metadata = new Dictionary<string, object?>();

        lock (_extensionMethods)
        {
            if (_extensionMethods.TryGetValue(methodInfo, out var extendedType))
            {
                metadata["ExtendedType"] = extendedType.FullName;
                metadata["IsExtensionMethod"] = true;
            }
            else
            {
                metadata["IsExtensionMethod"] = false;
                return metadata;
            }
        }

        lock (_invocationCounts)
        {
            metadata["InvocationCount"] = _invocationCounts.TryGetValue(methodInfo, out var count) ? count : 0;
        }

        metadata["DeclaringType"] = methodInfo.DeclaringType?.FullName;
        metadata["MethodName"] = methodInfo.Name;

        return metadata;
    }
}

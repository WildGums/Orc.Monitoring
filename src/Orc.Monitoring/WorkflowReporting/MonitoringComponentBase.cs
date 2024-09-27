namespace Orc.Monitoring.Reporters;

using System;
using System.Collections.Generic;
using Core.Abstractions;

public abstract class MonitoringComponentBase : IMonitoringComponent
{
    private readonly Dictionary<Type, Func<IMonitoringComponent>> _componentFactories = new();
    private readonly Dictionary<Type, IMonitoringComponent> _components = new();

    public void AddComponent<T>(Func<T> componentFactory) where T : IMonitoringComponent
    {
        _componentFactories.Add(typeof(T), () => componentFactory());
    }

    public IEnumerable<IMonitoringComponent> GetComponents()
    {
        foreach (var (componentType, factory) in _componentFactories)
        {
            if (!_components.TryGetValue(componentType, out var component))
            {
                component = factory();
                _components.Add(componentType, component);
            }

            yield return component;
        }
    }
}

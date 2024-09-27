namespace Orc.Monitoring.Core.Abstractions;

using System;
using System.Collections.Generic;

public interface IMonitoringComponent
{
    void AddComponent<T>(Func<T> componentFactory) where T : IMonitoringComponent;
    IEnumerable<IMonitoringComponent> GetComponents();
}


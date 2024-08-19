namespace Orc.Monitoring.Configuration;

using System;
using System.Collections.Generic;
using System.Reflection;
using Filters;

public class TypeAndMethodTracker
{
    private readonly HashSet<Type> _trackedTypes = [];
    private readonly List<IMethodFilter> _filters = [];

    public TypeAndMethodTracker TrackAssembly(Assembly assembly)
    {
        foreach (var type in assembly.GetTypes())
        {
            TrackType(type);
        }

        return this;
    }

    public TypeAndMethodTracker TrackType(Type type)
    {
        if (!_trackedTypes.Add(type))
        {
            return this;
        }

        return this;
    }

    public TypeAndMethodTracker AddFilter(IMethodFilter filter)
    {
        _filters.Add(filter);

        return this;
    }
}

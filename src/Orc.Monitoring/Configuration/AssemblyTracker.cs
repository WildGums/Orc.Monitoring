﻿namespace Orc.Monitoring.Configuration;

using System;
using System.Collections.Generic;
using System.Reflection;
using Orc.Monitoring.Filters;


public class AssemblyTracker
{
    private readonly HashSet<Type> _trackedTypes = [];
    private readonly List<IMethodFilter> _filters = [];

    public AssemblyTracker TrackAssembly(Assembly assembly)
    {
        foreach (var type in assembly.GetTypes())
        {
            TrackType(type);
        }

        return this;
    }

    public AssemblyTracker TrackType(Type type)
    {
        if (!_trackedTypes.Add(type))
        {
            return this;
        }

        return this;
    }

    public AssemblyTracker AddFilter(IMethodFilter filter)
    {
        _filters.Add(filter);

        return this;
    }

    public bool IsTypeTracked(Type type)
    {
        return _trackedTypes.Contains(type);
    }

    public IEnumerable<IMethodFilter> GetFilters()
    {
        return _filters;
    }
}

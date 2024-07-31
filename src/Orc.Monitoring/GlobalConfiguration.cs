namespace Orc.Monitoring;

using System.Collections.Generic;
using System.Reflection;
using Filters;

public class GlobalConfiguration
{
    public List<IMethodFilter> Filters { get; set; } = new List<IMethodFilter>();
    public List<Assembly> TrackedAssemblies { get; set; } = new List<Assembly>();
}

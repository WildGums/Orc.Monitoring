namespace Orc.Monitoring;

using System.Reflection;
using Filters;

public class GlobalConfigurationBuilder
{
    private readonly GlobalConfiguration _config = new GlobalConfiguration();

    public GlobalConfigurationBuilder AddFilter(IMethodFilter filter)
    {
        _config.Filters.Add(filter);
        return this;
    }

    public GlobalConfigurationBuilder TrackAssembly(Assembly assembly)
    {
        _config.TrackedAssemblies.Add(assembly);
        return this;
    }

    public GlobalConfiguration Build() => _config;
}

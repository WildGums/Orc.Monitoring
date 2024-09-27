namespace Orc.Monitoring.Reporters;

using Orc.Monitoring.Core.Abstractions;
using System;

public static class IMethodCallReporterExtensions
{
    public static IMethodCallReporter AddFilter(this IMethodCallReporter methodCallReporter, Func<IMethodFilter> componentFactory)
    {
        methodCallReporter.AddComponent(componentFactory);

        return methodCallReporter;
    }

    public static IMethodCallReporter AddOutput(this IMethodCallReporter methodCallReporter, Func<IReportOutput> componentFactory)
    {
        methodCallReporter.AddComponent(componentFactory);

        return methodCallReporter;
    }
}

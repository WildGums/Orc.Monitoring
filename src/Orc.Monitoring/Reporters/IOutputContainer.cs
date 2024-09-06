// ReSharper disable InconsistentNaming
namespace Orc.Monitoring.Reporters;

using Filters;
using ReportOutputs;

public interface IOutputContainer
{
    IOutputContainer AddOutput<TOutput>(object? parameter = null) where TOutput : IReportOutput, new();
    IOutputContainer AddFilter<T>() where T : IMethodFilter;
}

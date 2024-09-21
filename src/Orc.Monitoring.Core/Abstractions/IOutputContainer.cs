// ReSharper disable InconsistentNaming
namespace Orc.Monitoring.Core.Abstractions;

public interface IOutputContainer
{
    IOutputContainer AddOutput<TOutput>(object? parameter = null) where TOutput : IReportOutput, new();
    IOutputContainer AddFilter<T>() where T : IMethodFilter;
}

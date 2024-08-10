// ReSharper disable InconsistentNaming
namespace Orc.Monitoring.Reporters;

using ReportOutputs;

public interface IOutputContainer
{
    IOutputContainer AddOutput<TOutput>(object? parameter = null) where TOutput : IReportOutput, new();
}

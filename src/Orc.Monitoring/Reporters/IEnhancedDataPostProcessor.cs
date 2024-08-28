namespace Orc.Monitoring.Reporters;

using System.Collections.Generic;
using ReportOutputs;

public interface IEnhancedDataPostProcessor
{
    List<ReportItem> PostProcessData(List<ReportItem> items, EnhancedDataPostProcessor.OrphanedNodeStrategy strategy);
}

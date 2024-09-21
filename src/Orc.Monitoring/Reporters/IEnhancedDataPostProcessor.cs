namespace Orc.Monitoring.Reporters;

using System.Collections.Generic;
using Core.Models;
using Monitoring.Reporters.ReportOutputs;

public interface IEnhancedDataPostProcessor
{
    List<ReportItem> PostProcessData(List<ReportItem> items);
}

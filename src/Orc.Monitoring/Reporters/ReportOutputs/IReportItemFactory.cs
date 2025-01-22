namespace Orc.Monitoring.Reporters.ReportOutputs;

using System.Collections.Generic;
using MethodLifeCycleItems;

public interface IReportItemFactory
{
    ReportItem CloneReportItemWithOverrides(ReportItem reportItem, MethodOverrideManager methodOverrideManager);
    ReportItem CreateReportItem(IMethodLifeCycleItem lifeCycleItem, IMethodCallReporter? reporter);
    ReportItem UpdateReportItemEnding(MethodCallEnd end, IMethodCallReporter? reporter, Dictionary<string, ReportItem> existingReportItems);
    ReportItem CreateGapReportItem(CallGap gap, IMethodCallReporter? reporter);
}

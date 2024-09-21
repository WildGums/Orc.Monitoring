namespace Orc.Monitoring.Core.Factories;

using System.Collections.Generic;
using Orc.Monitoring.Core.Abstractions;
using Orc.Monitoring.Core.MethodLifecycle;
using Orc.Monitoring.Core.Models;

public interface IReportItemFactory
{
    ReportItem CloneReportItemWithOverrides(ReportItem reportItem, Dictionary<string, string> overrides);
    ReportItem CreateReportItem(IMethodLifeCycleItem lifeCycleItem, IMethodCallReporter? reporter);
    ReportItem UpdateReportItemEnding(MethodCallEnd end, IMethodCallReporter? reporter, Dictionary<string, ReportItem> existingReportItems);
    ReportItem CreateGapReportItem(CallGap gap, IMethodCallReporter? reporter);
}

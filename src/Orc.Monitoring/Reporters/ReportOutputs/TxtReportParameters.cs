namespace Orc.Monitoring.Reporters.ReportOutputs;

using System;

public class TxtReportParameters
{
    public string FolderPath { get; }
    public string DisplayNameParameter { get; }
    public OutputLimitOptions LimitOptions { get; set; }

    public TxtReportParameters(string folderPath, string displayNameParameter, OutputLimitOptions? limitOptions = null)
    {
        FolderPath = folderPath;
        DisplayNameParameter = displayNameParameter;
        LimitOptions = limitOptions ?? OutputLimitOptions.Unlimited;
    }
}

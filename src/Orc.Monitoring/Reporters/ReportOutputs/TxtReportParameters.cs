namespace Orc.Monitoring.Reporters.ReportOutputs;

public class TxtReportParameters(string folderPath, string displayNameParameter, OutputLimitOptions? limitOptions = null)
{
    public string FolderPath { get; } = folderPath;
    public string DisplayNameParameter { get; } = displayNameParameter;
    public OutputLimitOptions LimitOptions { get; set; } = limitOptions ?? OutputLimitOptions.Unlimited;
}

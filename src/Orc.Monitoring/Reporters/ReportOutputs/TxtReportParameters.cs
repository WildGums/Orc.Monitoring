namespace Orc.Monitoring.Reporters.ReportOutputs;

using System;

public class TxtReportParameters
{
    public TxtReportParameters(string folderPath, string displayNameParameter)
    {
        ArgumentNullException.ThrowIfNull(folderPath);
        ArgumentNullException.ThrowIfNull(displayNameParameter);

        FolderPath = folderPath;
        DisplayNameParameter = displayNameParameter;
    }

    public string FolderPath { get; }
    public string DisplayNameParameter { get; }
}

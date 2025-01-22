namespace Orc.Monitoring.Reporters.ReportOutputs;

/// <summary>
/// Represents the parameters for configuring a Rantt report output.
/// </summary>
public class RanttReportParameters
{
    /// <summary>
    /// Gets or sets the folder path where the Rantt report files will be saved.
    /// </summary>
    public string? FolderPath { get; set; }

    /// <summary>
    /// Gets or sets the output limit options for the Rantt report.
    /// </summary>
    public OutputLimitOptions LimitOptions { get; set; } = OutputLimitOptions.Unlimited;

    /// <summary>
    /// Initializes a new instance of the <see cref="RanttReportParameters"/> class.
    /// </summary>
    public RanttReportParameters()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RanttReportParameters"/> class with specified values.
    /// </summary>
    /// <param name="folderPath">The folder path where the Rantt report files will be saved.</param>
    /// <param name="limitOptions">The output limit options for the Rantt report.</param>
    public RanttReportParameters(string folderPath, OutputLimitOptions? limitOptions = null)
    {
        FolderPath = folderPath;
        LimitOptions = limitOptions ?? OutputLimitOptions.Unlimited;
    }

    /// <summary>
    /// Creates a new instance of RanttReportParameters with the specified folder path, optional limit options, and orphaned node strategy.
    /// </summary>
    /// <param name="folderPath">The folder path where the Rantt report files will be saved.</param>
    /// <param name="limitOptions">The output limit options for the Rantt report. If null, no limits will be applied.</param>
    /// <returns>A new instance of RanttReportParameters.</returns>
    public static RanttReportParameters Create(
        string folderPath,
        OutputLimitOptions? limitOptions = null)
    {
        return new RanttReportParameters(folderPath, limitOptions);
    }
}

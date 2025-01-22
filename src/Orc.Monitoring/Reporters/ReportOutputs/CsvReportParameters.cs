namespace Orc.Monitoring.Reporters.ReportOutputs;

/// <summary>
/// Represents the parameters for configuring a CSV report output.
/// </summary>
public class CsvReportParameters
{
    /// <summary>
    /// Gets or sets the folder path where the CSV file will be saved.
    /// </summary>
    public string? FolderPath { get; set; }

    /// <summary>
    /// Gets or sets the file name for the CSV report (without extension).
    /// </summary>
    public string? FileName { get; set; }

    /// <summary>
    /// Gets or sets the output limit options for the CSV report.
    /// </summary>
    public OutputLimitOptions LimitOptions { get; set; } = OutputLimitOptions.Unlimited;

    /// <summary>
    /// Initializes a new instance of the <see cref="CsvReportParameters"/> class.
    /// </summary>
    public CsvReportParameters()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CsvReportParameters"/> class with specified values.
    /// </summary>
    /// <param name="folderPath">The folder path where the CSV file will be saved.</param>
    /// <param name="fileName">The file name for the CSV report (without extension).</param>
    /// <param name="limitOptions">The output limit options for the CSV report.</param>
    public CsvReportParameters(string folderPath, string fileName, OutputLimitOptions? limitOptions = null)
    {
        FolderPath = folderPath;
        FileName = fileName;
        LimitOptions = limitOptions ?? OutputLimitOptions.Unlimited;
    }
}

namespace Orc.Monitoring.Reporters.ReportOutputs;

/// <summary>
/// Defines methods for report outputs that support item limits.
/// </summary>
public interface ILimitableOutput
{
    /// <summary>
    /// Sets the limit options for the output.
    /// </summary>
    /// <param name="options">The output limit options to set.</param>
    void SetLimitOptions(OutputLimitOptions options);

    /// <summary>
    /// Gets the current limit options for the output.
    /// </summary>
    /// <returns>The current output limit options.</returns>
    OutputLimitOptions GetLimitOptions();
}

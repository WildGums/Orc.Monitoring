namespace Orc.Monitoring.Reporters.ReportOutputs;

/// <summary>
/// Represents options for limiting output in reporting.
/// </summary>
public class OutputLimitOptions
{
    /// <summary>
    /// Gets or sets the maximum number of items to include in the output.
    /// If null, there is no limit on the number of items.
    /// </summary>
    public int? MaxItems { get; set; }

    /// <summary>
    /// Gets a value indicating whether any limits are set.
    /// </summary>
    public bool HasLimits => MaxItems.HasValue;

    /// <summary>
    /// Gets the default unlimited options.
    /// </summary>
    public static OutputLimitOptions Unlimited => new OutputLimitOptions();

    /// <summary>
    /// Creates a new instance of OutputLimitOptions with a maximum number of items.
    /// </summary>
    /// <param name="maxItems">The maximum number of items to include in the output.</param>
    /// <returns>A new OutputLimitOptions instance.</returns>
    public static OutputLimitOptions LimitItems(int maxItems)
    {
        return new OutputLimitOptions { MaxItems = maxItems };
    }
}

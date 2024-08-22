namespace Orc.Monitoring.Reporters.ReportOutputs;

using System;

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
    /// Gets or sets the maximum age of items to include in the output.
    /// If null, there is no limit on the age of items.
    /// </summary>
    public TimeSpan? MaxAge { get; set; }

    /// <summary>
    /// Gets a value indicating whether any limits are set.
    /// </summary>
    public bool HasLimits => MaxItems.HasValue || MaxAge.HasValue;

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

    /// <summary>
    /// Creates a new instance of OutputLimitOptions with a maximum age for items.
    /// </summary>
    /// <param name="maxAge">The maximum age of items to include in the output.</param>
    /// <returns>A new OutputLimitOptions instance.</returns>
    public static OutputLimitOptions LimitAge(TimeSpan maxAge)
    {
        return new OutputLimitOptions { MaxAge = maxAge };
    }

    /// <summary>
    /// Creates a new instance of OutputLimitOptions with both item count and age limits.
    /// </summary>
    /// <param name="maxItems">The maximum number of items to include in the output.</param>
    /// <param name="maxAge">The maximum age of items to include in the output.</param>
    /// <returns>A new OutputLimitOptions instance.</returns>
    public static OutputLimitOptions Limit(int maxItems, TimeSpan maxAge)
    {
        return new OutputLimitOptions { MaxItems = maxItems, MaxAge = maxAge };
    }
}

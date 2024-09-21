namespace Orc.Monitoring.Filters;

using System;
using System.Collections.Generic;
using Core.Abstractions;
using Core.Models;
using Reporters;

/// <summary>
/// Filters method calls based on their granularity level.
/// </summary>
public class WorkflowItemGranularityFilter : IMethodFilter
{
    private readonly int _level;

    private readonly List<string> _sortedLevers =
    [
        MethodCallParameter.Granularity.Fine,
        MethodCallParameter.Granularity.Medium,
        MethodCallParameter.Granularity.Coarse
    ];

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowItemGranularityFilter"/> class.
    /// </summary>
    /// <param name="level">The granularity level.</param>
    /// <exception cref="ArgumentException">Thrown when the provided level is invalid.</exception>
    public WorkflowItemGranularityFilter(string level)
    {
        _level = _sortedLevers.IndexOf(level);
        if (_level == -1)
        {
            throw new ArgumentException($"Invalid granularity level: {level}", nameof(level));
        }
    }

    /// <summary>
    /// Determines whether the specified method call should be included based on its granularity level.
    /// </summary>
    /// <param name="methodCallInfo">The method call information.</param>
    /// <returns><c>true</c> if the method call should be included; otherwise, <c>false</c>.</returns>
    public bool ShouldInclude(MethodCallInfo methodCallInfo)
    {
        if (methodCallInfo.Parameters is null)
        {
            return true;
        }

        var level = methodCallInfo.Parameters.GetValueOrDefault(MethodCallParameter.WorkflowItemGranularity);
        return ShouldIncludeInternal(level);
    }

    private bool ShouldIncludeInternal(string? level)
    {
        if (level is null)
        {
            return true;
        }

        var methodLevel = _sortedLevers.IndexOf(level);

        return methodLevel >= _level;
    }
}

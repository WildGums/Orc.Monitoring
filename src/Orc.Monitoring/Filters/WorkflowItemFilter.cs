﻿namespace Orc.Monitoring.Filters;

using System;
using System.Collections.Generic;
using System.Linq;
using Core.Abstractions;
using Core.Models;
using Monitoring;
using Reporters;

public class WorkflowItemFilter : MonitoringComponentBase, IMethodFilter
{
    public bool ShouldInclude(MethodCallInfo methodCallInfo)
    {
        if (methodCallInfo.Parameters is null)
        {
            return false;
        }

        return methodCallInfo.Parameters
            .Any(p => string.Equals(p.Key, MethodCallParameter.WorkflowItemName, StringComparison.OrdinalIgnoreCase)
                      && !string.IsNullOrWhiteSpace(p.Value));
    }
}

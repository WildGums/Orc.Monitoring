namespace Orc.Monitoring.Benchmarks;

using BenchmarkDotNet.Attributes;
using System;
using System.Collections.Generic;
using Filters;
using Reporters;

[MemoryDiagnoser]
public class FilterBenchmarks
{
    private WorkflowItemFilter? _workflowItemFilter;
    private MethodCallInfo? _methodCallInfoWithWorkflowItem;
    private MethodCallInfo? _methodCallInfoWithoutWorkflowItem;
    private MethodCallInfo? _methodCallInfoWithEmptyWorkflowItem;

    [GlobalSetup]
    public void Setup()
    {
        _workflowItemFilter = new WorkflowItemFilter();

        _methodCallInfoWithWorkflowItem = new MethodCallInfo
        {
            Parameters = new Dictionary<string, string>
            {
                { MethodCallParameter.WorkflowItemName, "TestWorkflowItem" }
            }
        };

        _methodCallInfoWithoutWorkflowItem = new MethodCallInfo
        {
            Parameters = new Dictionary<string, string>
            {
                { "SomeOtherParameter", "SomeValue" }
            }
        };

        _methodCallInfoWithEmptyWorkflowItem = new MethodCallInfo
        {
            Parameters = new Dictionary<string, string>
            {
                { MethodCallParameter.WorkflowItemName, string.Empty }
            }
        };
    }

    [Benchmark]
    public bool WorkflowItemFilterWithWorkflowItem()
    {
        return _workflowItemFilter!.ShouldInclude(_methodCallInfoWithWorkflowItem!);
    }

    [Benchmark]
    public bool WorkflowItemFilterWithoutWorkflowItem()
    {
        return _workflowItemFilter!.ShouldInclude(_methodCallInfoWithoutWorkflowItem!);
    }

    [Benchmark]
    public bool WorkflowItemFilterWithEmptyWorkflowItem()
    {
        return _workflowItemFilter!.ShouldInclude(_methodCallInfoWithEmptyWorkflowItem!);
    }

    [Benchmark]
    public void WorkflowItemFilterMultipleCalls()
    {
        _workflowItemFilter!.ShouldInclude(_methodCallInfoWithWorkflowItem!);
        _workflowItemFilter.ShouldInclude(_methodCallInfoWithoutWorkflowItem!);
        _workflowItemFilter!.ShouldInclude(_methodCallInfoWithEmptyWorkflowItem!);
    }
}

namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using Filters;
using Reporters;
using System.Collections.Generic;

[TestFixture]
public class WorkflowItemFilterTests
{
    private WorkflowItemFilter _filter;

    [SetUp]
    public void Setup()
    {
        _filter = new WorkflowItemFilter();
    }

    [Test]
    public void ShouldInclude_WithWorkflowItemName_ReturnsTrue()
    {
        var methodCallInfo = new MethodCallInfo
        {
            Parameters = new Dictionary<string, string>
            {
                { MethodCallParameter.WorkflowItemName, "TestWorkflowItem" }
            }
        };

        Assert.That(_filter.ShouldInclude(methodCallInfo), Is.True);
    }

    [Test]
    public void ShouldInclude_WithoutWorkflowItemName_ReturnsFalse()
    {
        var methodCallInfo = new MethodCallInfo
        {
            Parameters = new Dictionary<string, string>()
        };

        Assert.That(_filter.ShouldInclude(methodCallInfo), Is.False);
    }

    [Test]
    public void ShouldInclude_WithEmptyWorkflowItemName_ReturnsFalse()
    {
        var methodCallInfo = new MethodCallInfo
        {
            Parameters = new Dictionary<string, string>
            {
                { MethodCallParameter.WorkflowItemName, string.Empty }
            }
        };

        Assert.That(_filter.ShouldInclude(methodCallInfo), Is.False);
    }

    [Test]
    public void ShouldInclude_WithWhitespaceWorkflowItemName_ReturnsFalse()
    {
        var methodCallInfo = new MethodCallInfo
        {
            Parameters = new Dictionary<string, string>
            {
                { MethodCallParameter.WorkflowItemName, "   " }
            }
        };

        Assert.That(_filter.ShouldInclude(methodCallInfo), Is.False);
    }

    [Test]
    public void ShouldInclude_WithNullParameters_ReturnsFalse()
    {
        var methodCallInfo = new MethodCallInfo
        {
            Parameters = null
        };

        Assert.That(_filter.ShouldInclude(methodCallInfo), Is.False);
    }

    [Test]
    public void ShouldInclude_WithOtherParameters_ReturnsFalse()
    {
        var methodCallInfo = new MethodCallInfo
        {
            Parameters = new Dictionary<string, string>
            {
                { "OtherParameter", "SomeValue" }
            }
        };

        Assert.That(_filter.ShouldInclude(methodCallInfo), Is.False);
    }

    [Test]
    public void ShouldInclude_WithWorkflowItemNameAndOtherParameters_ReturnsTrue()
    {
        var methodCallInfo = new MethodCallInfo
        {
            Parameters = new Dictionary<string, string>
            {
                { MethodCallParameter.WorkflowItemName, "TestWorkflowItem" },
                { "OtherParameter", "SomeValue" }
            }
        };

        Assert.That(_filter.ShouldInclude(methodCallInfo), Is.True);
    }

    [Test]
    public void ShouldInclude_WithCaseInsensitiveWorkflowItemName_ReturnsTrue()
    {
        var methodCallInfo = new MethodCallInfo
        {
            Parameters = new Dictionary<string, string>
            {
                { "workflowitemname", "TestWorkflowItem" }
            }
        };

        Assert.That(_filter.ShouldInclude(methodCallInfo), Is.True);
    }
}

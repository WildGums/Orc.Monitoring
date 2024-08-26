namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using Orc.Monitoring.Reporters.ReportOutputs;
using System;

[TestFixture]
public class OutputLimitOptionsTests
{
    [Test]
    public void DefaultConstructor_CreatesUnlimitedOptions()
    {
        var options = new OutputLimitOptions();

        Assert.That(options.MaxItems, Is.Null);
        Assert.That(options.HasLimits, Is.False);
    }

    [Test]
    public void LimitItems_CreatesOptionsWithMaxItems()
    {
        var options = OutputLimitOptions.LimitItems(100);

        Assert.That(options.MaxItems, Is.EqualTo(100));
        Assert.That(options.HasLimits, Is.True);
    }

    [Test]
    public void Unlimited_ReturnsUnlimitedOptions()
    {
        var options = OutputLimitOptions.Unlimited;

        Assert.That(options.MaxItems, Is.Null);
        Assert.That(options.HasLimits, Is.False);
    }

    [Test]
    public void HasLimits_ReturnsTrueWhenMaxItemsIsSet()
    {
        var options = new OutputLimitOptions { MaxItems = 50 };

        Assert.That(options.HasLimits, Is.True);
    }

    [Test]
    public void HasLimits_ReturnsFalseWhenNoLimitsAreSet()
    {
        var options = new OutputLimitOptions();

        Assert.That(options.HasLimits, Is.False);
    }

    [Test]
    public void MaxItems_CanBeSetToZero()
    {
        var options = new OutputLimitOptions { MaxItems = 0 };

        Assert.That(options.MaxItems, Is.EqualTo(0));
        Assert.That(options.HasLimits, Is.True);
    }

    [Test]
    public void MaxItems_CannotBeSetToNegativeValue()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new OutputLimitOptions { MaxItems = -1 });
    }

    [Test]
    public void Equals_ReturnsTrueForEqualOptions()
    {
        var options1 = new OutputLimitOptions { MaxItems = 100 };
        var options2 = new OutputLimitOptions { MaxItems = 100 };

        Assert.That(options1, Is.EqualTo(options2));
    }

    [Test]
    public void Equals_ReturnsFalseForDifferentOptions()
    {
        var options1 = new OutputLimitOptions { MaxItems = 100 };
        var options2 = new OutputLimitOptions { MaxItems = 200 };

        Assert.That(options1, Is.Not.EqualTo(options2));
    }

    [Test]
    public void GetHashCode_ReturnsSameValueForEqualOptions()
    {
        var options1 = new OutputLimitOptions { MaxItems = 100 };
        var options2 = new OutputLimitOptions { MaxItems = 100 };

        Assert.That(options1.GetHashCode(), Is.EqualTo(options2.GetHashCode()));
    }
}

namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using Reporters.ReportOutputs;
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
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            _ = new OutputLimitOptions { MaxItems = -1 };
        });
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


    [Test]
    public void MaxItems_CanBeSetToMaxIntValue()
    {
        var options = new OutputLimitOptions { MaxItems = int.MaxValue };

        Assert.That(options.MaxItems, Is.EqualTo(int.MaxValue));
        Assert.That(options.HasLimits, Is.True);
    }

    [Test]
    public void MaxItems_CanBeSetToOne()
    {
        var options = new OutputLimitOptions { MaxItems = 1 };

        Assert.That(options.MaxItems, Is.EqualTo(1));
        Assert.That(options.HasLimits, Is.True);
    }

    [Test]
    public void LimitItems_WithZero_CreatesOptionsWithZeroMaxItems()
    {
        var options = OutputLimitOptions.LimitItems(0);

        Assert.That(options.MaxItems, Is.EqualTo(0));
        Assert.That(options.HasLimits, Is.True);
    }

    [Test]
    public void LimitItems_WithMaxIntValue_CreatesOptionsWithMaxIntValueMaxItems()
    {
        var options = OutputLimitOptions.LimitItems(int.MaxValue);

        Assert.That(options.MaxItems, Is.EqualTo(int.MaxValue));
        Assert.That(options.HasLimits, Is.True);
    }

    [Test]
    public void LimitItems_WithNegativeValue_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => OutputLimitOptions.LimitItems(-1));
    }

    [Test]
    public void Equals_ReturnsTrueForBothUnlimitedOptions()
    {
        var options1 = OutputLimitOptions.Unlimited;
        var options2 = new OutputLimitOptions();

        Assert.That(options1, Is.EqualTo(options2));
    }

    [Test]
    public void Equals_ReturnsFalseForUnlimitedAndLimitedOptions()
    {
        var unlimited = OutputLimitOptions.Unlimited;
        var limited = OutputLimitOptions.LimitItems(100);

        Assert.That(unlimited, Is.Not.EqualTo(limited));
    }

    [Test]
    public void GetHashCode_ReturnsDifferentValuesForDifferentOptions()
    {
        var options1 = new OutputLimitOptions { MaxItems = 100 };
        var options2 = new OutputLimitOptions { MaxItems = 200 };

        Assert.That(options1.GetHashCode(), Is.Not.EqualTo(options2.GetHashCode()));
    }
}

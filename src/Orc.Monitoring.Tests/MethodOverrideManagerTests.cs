namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using Orc.Monitoring.Reporters.ReportOutputs;
using System.IO;

[TestFixture]
public class MethodOverrideManagerTests
{
    private string _tempDirectory;
    private string _overrideFilePath;
    private MethodOverrideManager _overrideManager;

    [SetUp]
    public void Setup()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDirectory);
        _overrideFilePath = Path.Combine(_tempDirectory, "method_overrides.csv");
        _overrideManager = new MethodOverrideManager(_tempDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }

    [Test]
    public void LoadOverrides_LoadsCorrectly()
    {
        // Arrange
        var csvContent = @"FullName,IsStatic,IsExtension,CustomColumn1,CustomColumn2
TestNamespace.TestClass.TestMethod,True,False,CustomValue1,CustomValue2
TestNamespace.TestClass.AnotherMethod,False,True,CustomValue3,CustomValue4";

        File.WriteAllText(_overrideFilePath, csvContent);

        // Act
        _overrideManager.LoadOverrides();

        // Assert
        var customColumns = _overrideManager.GetCustomColumns();
        Assert.That(customColumns, Does.Contain("CustomColumn1"));
        Assert.That(customColumns, Does.Contain("CustomColumn2"));

        var testMethodOverrides = _overrideManager.GetOverridesForMethod("TestNamespace.TestClass.TestMethod");
        Assert.That(testMethodOverrides, Does.ContainKey("IsStatic"));
        Assert.That(testMethodOverrides["IsStatic"], Is.EqualTo("True"));
        Assert.That(testMethodOverrides, Does.ContainKey("CustomColumn1"));
        Assert.That(testMethodOverrides["CustomColumn1"], Is.EqualTo("CustomValue1"));

        var anotherMethodOverrides = _overrideManager.GetOverridesForMethod("TestNamespace.TestClass.AnotherMethod");
        Assert.That(anotherMethodOverrides, Does.ContainKey("IsExtension"));
        Assert.That(anotherMethodOverrides["IsExtension"], Is.EqualTo("True"));
        Assert.That(anotherMethodOverrides, Does.ContainKey("CustomColumn2"));
        Assert.That(anotherMethodOverrides["CustomColumn2"], Is.EqualTo("CustomValue4"));
    }

    [Test]
    public void LoadOverrides_HandlesEmptyFile()
    {
        // Arrange
        File.WriteAllText(_overrideFilePath, string.Empty);

        // Act
        _overrideManager.LoadOverrides();

        // Assert
        var customColumns = _overrideManager.GetCustomColumns();
        Assert.That(customColumns, Is.Empty, "Custom columns should be empty for an empty file");

        var overrides = _overrideManager.GetOverridesForMethod("AnyMethod");
        Assert.That(overrides, Is.Empty, "Overrides should be empty for an empty file");
    }

    [Test]
    public void LoadOverrides_HandlesNonExistentFile()
    {
        // Act
        _overrideManager.LoadOverrides();

        // Assert
        var customColumns = _overrideManager.GetCustomColumns();
        Assert.That(customColumns, Is.Empty);

        var overrides = _overrideManager.GetOverridesForMethod("AnyMethod");
        Assert.That(overrides, Is.Empty);
    }

    [Test]
    public void LoadOverrides_ClearsExistingOverridesWhenFileIsEmpty()
    {
        // Arrange
        var initialCsvContent = "FullName,CustomColumn\nTestMethod,CustomValue";
        File.WriteAllText(_overrideFilePath, initialCsvContent);
        _overrideManager.LoadOverrides();

        // Verify initial state
        Assert.That(_overrideManager.GetCustomColumns(), Is.Not.Empty, "Should have custom columns initially");
        Assert.That(_overrideManager.GetOverridesForMethod("TestMethod"), Is.Not.Empty, "Should have overrides initially");

        // Act
        File.WriteAllText(_overrideFilePath, string.Empty);
        _overrideManager.LoadOverrides();

        // Assert
        var customColumns = _overrideManager.GetCustomColumns();
        Assert.That(customColumns, Is.Empty, "Custom columns should be cleared after loading empty file");

        var overrides = _overrideManager.GetOverridesForMethod("TestMethod");
        Assert.That(overrides, Is.Empty, "Overrides should be cleared after loading empty file");
    }
}

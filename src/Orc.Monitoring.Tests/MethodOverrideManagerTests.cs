namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using Orc.Monitoring.Reporters.ReportOutputs;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

[TestFixture]
public class MethodOverrideManagerTests
{
    private string _tempDirectory;
    private string _overrideFilePath;
    private string _overrideTemplateFilePath;
    private MethodOverrideManager _overrideManager;

    [SetUp]
    public void Setup()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDirectory);
        _overrideFilePath = Path.Combine(_tempDirectory, "method_overrides.csv");
        _overrideTemplateFilePath = Path.Combine(_tempDirectory, "method_overrides.template");
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
    public void SaveOverrides_CreatesTemplateFile()
    {
        // Arrange
        var reportItems = new List<ReportItem>
        {
            new ReportItem
            {
                FullName = "TestNamespace.TestClass.TestMethod",
                Parameters = new Dictionary<string, string> { { "CustomColumn", "CustomValue" } },
                AttributeParameters = new HashSet<string> { "CustomColumn" }
            }
        };

        // Act
        _overrideManager.SaveOverrides(reportItems);

        // Assert
        Assert.That(File.Exists(_overrideTemplateFilePath), Is.True, "Template file should be created");
        var templateContent = File.ReadAllText(_overrideTemplateFilePath);
        Assert.That(templateContent, Does.Contain("CustomColumn"), "Template should contain the custom column");
        Assert.That(templateContent, Does.Contain("CustomValue"), "Template should contain the custom value");
    }

    [Test]
    public void SaveOverrides_DoesNotModifyCsvFile()
    {
        // Arrange
        var initialCsvContent = "FullName,CustomColumn\nTestMethod,InitialValue";
        File.WriteAllText(_overrideFilePath, initialCsvContent);

        var reportItems = new List<ReportItem>
        {
            new ReportItem
            {
                FullName = "TestMethod",
                Parameters = new Dictionary<string, string> { { "CustomColumn", "NewValue" } },
                AttributeParameters = new HashSet<string> { "CustomColumn" }
            }
        };

        // Act
        _overrideManager.SaveOverrides(reportItems);

        // Assert
        var csvContent = File.ReadAllText(_overrideFilePath);
        Assert.That(csvContent, Is.EqualTo(initialCsvContent), "CSV file should not be modified");
    }

    [Test]
    public void GetOverridesForMethod_ReturnsCorrectOverrides()
    {
        // Arrange
        var csvContent = "FullName,CustomColumn\nTestMethod,CustomValue";
        File.WriteAllText(_overrideFilePath, csvContent);
        _overrideManager.LoadOverrides();

        // Act
        var overrides = _overrideManager.GetOverridesForMethod("TestMethod");

        // Assert
        Assert.That(overrides, Does.ContainKey("CustomColumn"));
        Assert.That(overrides["CustomColumn"], Is.EqualTo("CustomValue"));
    }

    [Test]
    public void GetCustomColumns_ReturnsCorrectColumns()
    {
        // Arrange
        var csvContent = "FullName,CustomColumn1,CustomColumn2\nTestMethod,Value1,Value2";
        File.WriteAllText(_overrideFilePath, csvContent);
        _overrideManager.LoadOverrides();

        // Act
        var customColumns = _overrideManager.GetCustomColumns();

        // Assert
        Assert.That(customColumns, Does.Contain("CustomColumn1"));
        Assert.That(customColumns, Does.Contain("CustomColumn2"));
    }

    [Test]
    public void SaveOverrides_UpdatesTemplateWithLatestData()
    {
        // Arrange
        var initialReportItems = new List<ReportItem>
        {
            new ReportItem
            {
                FullName = "TestMethod",
                Parameters = new Dictionary<string, string> { { "InitialColumn", "InitialValue" } },
                AttributeParameters = new HashSet<string> { "InitialColumn" }
            }
        };

        var updatedReportItems = new List<ReportItem>
        {
            new ReportItem
            {
                FullName = "TestMethod",
                Parameters = new Dictionary<string, string> { { "UpdatedColumn", "UpdatedValue" } },
                AttributeParameters = new HashSet<string> { "UpdatedColumn" }
            }
        };

        // Act
        _overrideManager.SaveOverrides(initialReportItems);
        _overrideManager.SaveOverrides(updatedReportItems);

        // Assert
        var templateContent = File.ReadAllText(_overrideTemplateFilePath);
        Assert.That(templateContent, Does.Contain("UpdatedColumn"), "Template should contain the updated column");
        Assert.That(templateContent, Does.Contain("UpdatedValue"), "Template should contain the updated value");
        Assert.That(templateContent, Does.Not.Contain("InitialColumn"), "Template should not contain the initial column");
    }
}

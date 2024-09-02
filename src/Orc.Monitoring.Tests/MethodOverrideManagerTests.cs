namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using Orc.Monitoring.Reporters.ReportOutputs;
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;

[TestFixture]
public class MethodOverrideManagerTests
{
    private string _testOutputPath;
    private string _overrideFilePath;
    private string _overrideTemplateFilePath;
    private MethodOverrideManager _overrideManager;
    private TestLogger<MethodOverrideManagerTests> _logger;
    private TestLoggerFactory<MethodOverrideManagerTests> _loggerFactory;
    private InMemoryFileSystem _fileSystem;
    private CsvUtils _csvUtils;

    [SetUp]
    public void Setup()
    {
        _logger = new TestLogger<MethodOverrideManagerTests>();
        _loggerFactory = new TestLoggerFactory<MethodOverrideManagerTests>(_logger);
        _fileSystem = new InMemoryFileSystem();
        _csvUtils = new CsvUtils(_fileSystem);

        _testOutputPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        _fileSystem.CreateDirectory(_testOutputPath);
        _overrideFilePath = Path.Combine(_testOutputPath, "method_overrides.csv");
        _overrideTemplateFilePath = Path.Combine(_testOutputPath, "method_overrides.template");
        _overrideManager = new MethodOverrideManager(_testOutputPath, _loggerFactory, _fileSystem, _csvUtils);
    }

    [TearDown]
    public void TearDown()
    {
        _fileSystem.Dispose();
        if (_fileSystem.DirectoryExists(_testOutputPath))
        {
            _fileSystem.DeleteDirectory(_testOutputPath, true);
        }
    }

    [Test]
    public void ReadOverrides_LoadsCorrectly()
    {
        // Arrange
        var csvContent = @"FullName,IsStatic,IsExtension,CustomColumn1,CustomColumn2
TestNamespace.TestClass.TestMethod,True,False,CustomValue1,CustomValue2
TestNamespace.TestClass.AnotherMethod,False,True,CustomValue3,CustomValue4";

        _fileSystem.WriteAllText(_overrideFilePath, csvContent);

        // Act
        _overrideManager.ReadOverrides();

        // Assert
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
    public void ReadOverrides_HandlesEmptyFile()
    {
        // Arrange
        _fileSystem.WriteAllText(_overrideFilePath, string.Empty);

        // Act
        _overrideManager.ReadOverrides();

        // Assert
        var overrides = _overrideManager.GetOverridesForMethod("AnyMethod");
        Assert.That(overrides, Is.Empty, "Overrides should be empty for an empty file");
    }

    [Test]
    public void ReadOverrides_HandlesNonExistentFile()
    {
        // Act
        _overrideManager.ReadOverrides();

        // Assert
        var overrides = _overrideManager.GetOverridesForMethod("AnyMethod");
        Assert.That(overrides, Is.Empty);
    }

    [Test]
    public void SaveOverrides_CreatesTemplateFileWithCorrectColumns()
    {
        // Arrange
        var reportItems = new List<ReportItem>
        {
            new ReportItem
            {
                FullName = "TestNamespace.TestClass.TestMethod",
                Parameters = new Dictionary<string, string>
                {
                    { "IsStatic", "True" },
                    { "IsExtension", "False" },
                    { "CustomColumn", "CustomValue" }
                }
            }
        };

        // Act
        _overrideManager.SaveOverrides(reportItems);

        // Assert
        Assert.That(_fileSystem.FileExists(_overrideTemplateFilePath), Is.True, "Template file should be created");
        var templateContent = _fileSystem.ReadAllText(_overrideTemplateFilePath);
        Assert.That(templateContent, Does.Contain("FullName,IsStatic,IsExtension"), "Template should contain standard columns");
        Assert.That(templateContent, Does.Contain("TestNamespace.TestClass.TestMethod,True,False"), "Template should contain correct values");
        Assert.That(templateContent, Does.Not.Contain("CustomColumn"), "Template should not contain custom columns");
    }

    [Test]
    public void GetOverridesForMethod_ReturnsCorrectOverrides()
    {
        // Arrange
        var csvContent = "FullName,CustomColumn\nTestMethod,CustomValue";
        _fileSystem.WriteAllText(_overrideFilePath, csvContent);
        _overrideManager.ReadOverrides();

        // Act
        var overrides = _overrideManager.GetOverridesForMethod("TestMethod");

        // Assert
        Assert.That(overrides, Does.ContainKey("CustomColumn"));
        Assert.That(overrides["CustomColumn"], Is.EqualTo("CustomValue"));
    }
}

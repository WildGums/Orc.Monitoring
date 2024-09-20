namespace Orc.Monitoring.Tests;

using System;
using NUnit.Framework;
using Reporters;
using Reporters.ReportOutputs;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TestUtilities.Logging;
using TestUtilities.Mocks;

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
        _fileSystem = new InMemoryFileSystem(_loggerFactory);
        _csvUtils = new CsvUtils(_fileSystem);

        _testOutputPath = _fileSystem.Combine(_fileSystem.GetTempPath(), _fileSystem.GetRandomFileName());
        _fileSystem.CreateDirectory(_testOutputPath);
        _overrideFilePath = _fileSystem.Combine(_testOutputPath, "method_overrides.csv");
        _overrideTemplateFilePath = _fileSystem.Combine(_testOutputPath, "method_overrides.template");
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
    [TestCase("TestNamespace.TestClass.TestMethod,True,False,CustomValue1,CustomValue2",
              "TestNamespace.TestClass.TestMethod", "IsStatic", "True")]
    [TestCase("TestNamespace.TestClass.AnotherMethod,False,True,CustomValue3,CustomValue4",
              "TestNamespace.TestClass.AnotherMethod", "IsExtension", "True")]
    [TestCase("TestNamespace.TestClass.GenericMethod<T>,False,False,CustomValue5,CustomValue6",
              "TestNamespace.TestClass.GenericMethod<T>", "CustomColumn1", "CustomValue5")]
    public void ReadOverrides_LoadsCorrectly(string csvContent, string methodName, string expectedKey, string expectedValue)
    {
        // Arrange
        var fullCsvContent = $"FullName,IsStatic,IsExtension,CustomColumn1,CustomColumn2\n{csvContent}";
        _fileSystem.WriteAllText(_overrideFilePath, fullCsvContent);

        // Act
        _overrideManager.ReadOverrides();

        // Assert
        var overrides = _overrideManager.GetOverridesForMethod(methodName, _ => true);
        Assert.That(overrides, Does.ContainKey(expectedKey));
        Assert.That(overrides[expectedKey], Is.EqualTo(expectedValue));
    }

    [Test]
    public void ReadOverrides_HandlesEmptyFile()
    {
        // Arrange
        _fileSystem.WriteAllText(_overrideFilePath, string.Empty);

        // Act
        _overrideManager.ReadOverrides();

        // Assert
        var overrides = _overrideManager.GetOverridesForMethod("AnyMethod", _ => true);
        Assert.That(overrides, Is.Empty, "Overrides should be empty for an empty file");
    }

    [Test]
    public void ReadOverrides_HandlesNonExistentFile()
    {
        // Act
        _overrideManager.ReadOverrides();

        // Assert
        var overrides = _overrideManager.GetOverridesForMethod("AnyMethod", _ => true);
        Assert.That(overrides, Is.Empty);
    }

    [Test]
    [TestCase("TestNamespace.TestClass.TestMethod", true, false, "CustomValue1")]
    [TestCase("TestNamespace.TestClass.AnotherMethod", false, true, "CustomValue2")]
    [TestCase("TestNamespace.TestClass.GenericMethod<T>", false, false, "CustomValue3")]
    public void SaveOverrides_CreatesTemplateFileWithCorrectColumns(string methodName, bool isStatic, bool isExtension, string customValue)
    {
        // Arrange
        var reportItems = new List<ReportItem>
        {
            new()
            {
                FullName = methodName,
                Parameters = new Dictionary<string, string>
                {
                    { "IsStatic", isStatic.ToString() },
                    { "IsExtension", isExtension.ToString() },
                    { "CustomColumn", customValue }
                }
            }
        };

        // Act
        _overrideManager.SaveOverrides(reportItems);

        // Assert
        Assert.That(_fileSystem.FileExists(_overrideTemplateFilePath), Is.True, "Template file should be created");
        var templateContent = _fileSystem.ReadAllLines(_overrideTemplateFilePath);

        var headerLine = templateContent[0];
        var headers = headerLine.Split(',').ToList();
        var methodNameIndex = headers.IndexOf("FullName");
        var isStaticIndex = headers.IndexOf("IsStatic");
        var isExtensionIndex = headers.IndexOf("IsExtension");
        var customColumnIndex = headers.IndexOf("CustomColumn");

        // assert headers exist
        Assert.That(methodNameIndex, Is.GreaterThanOrEqualTo(0), "FullName column should exist");
        Assert.That(isStaticIndex, Is.GreaterThanOrEqualTo(0), "IsStatic column should exist");
        Assert.That(isExtensionIndex, Is.GreaterThanOrEqualTo(0), "IsExtension column should exist");
        Assert.That(customColumnIndex, Is.GreaterThanOrEqualTo(0), "CustomColumn column should exist");

        // asserting data
        var dataLine = templateContent[1];
        var data = dataLine.Split(',').ToList();
        Assert.That(data[methodNameIndex], Is.EqualTo(methodName), "FullName should be correct");
        Assert.That(data[isStaticIndex], Is.EqualTo(isStatic.ToString()), "IsStatic should be correct");
        Assert.That(data[isExtensionIndex], Is.EqualTo(isExtension.ToString()), "IsExtension should be correct");
        Assert.That(data[customColumnIndex], Is.EqualTo(customValue), "CustomColumn should be correct");
    }

    [Test]
    [TestCase("TestMethod", "CustomColumn", "CustomValue")]
    [TestCase("AnotherMethod", "IsStatic", "True")]
    [TestCase("GenericMethod<T>", "IsExtension", "False")]
    public void GetOverridesForMethod_ReturnsCorrectOverrides(string methodName, string overrideKey, string overrideValue)
    {
        // Arrange
        var csvContent = $"FullName,{overrideKey}\n{methodName},{overrideValue}";
        _fileSystem.WriteAllText(_overrideFilePath, csvContent);
        _overrideManager.ReadOverrides();

        // Act
        var overrides = _overrideManager.GetOverridesForMethod(methodName, _ => true);

        // Assert
        Assert.That(overrides, Does.ContainKey(overrideKey));
        Assert.That(overrides[overrideKey], Is.EqualTo(overrideValue));
    }

    [Test]
    [TestCase("TestMethod,CustomValue1\nAnotherMethod,CustomValue2", 2)]
    [TestCase("SingleMethod,CustomValue", 1)]
    [TestCase("", 0)]
    public void ReadOverrides_HandlesVariousFileContents(string fileContent, int expectedOverrideCount)
    {
        // Arrange
        var fullContent = $"FullName,CustomColumn\n{fileContent}";
        _fileSystem.WriteAllText(_overrideFilePath, fullContent);

        // Act
        _overrideManager.ReadOverrides();

        // Assert
        var allOverrides = new List<Dictionary<string, string>>();
        foreach (var line in fileContent.Split('\n'))
        {
            if (!string.IsNullOrEmpty(line))
            {
                var parts = line.Split(',');
                allOverrides.Add(_overrideManager.GetOverridesForMethod(parts[0], _ => true));
            }
        }
        Assert.That(allOverrides.Count, Is.EqualTo(expectedOverrideCount));
        Assert.That(allOverrides.All(o => o.Count > 0), Is.True);
    }

    [Test]
    public void SaveOverrides_ExcludesGapsFromTemplate()
    {
        // Arrange
        var reportItems = new List<ReportItem>
        {
            new() { FullName = "Method1", MethodName = "Method1", Parameters = new Dictionary<string, string> { { "IsStatic", "True" } } },
            new() { FullName = "Method2", MethodName = "Method2", Parameters = new Dictionary<string, string> { { "IsExtension", "True" } } },
            new() { FullName = "Gap", MethodName = MethodCallParameter.Types.Gap, Parameters = new Dictionary<string, string>() }
        };

        // Act
        _overrideManager.SaveOverrides(reportItems);

        // Assert
        Assert.That(_fileSystem.FileExists(_overrideTemplateFilePath), Is.True, "Template file should be created");
        var templateContent = _fileSystem.ReadAllText(_overrideTemplateFilePath);
        var lines = templateContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.That(lines.Length, Is.EqualTo(3), "Template should have header and two data lines");
        var headerLine = lines[0].Trim();
        var headers = headerLine.Split(',').ToList();
        var methodNameIndex = headers.IndexOf("FullName");
        var isStaticIndex = headers.IndexOf("IsStatic");
        var isExtensionIndex = headers.IndexOf("IsExtension");

        // assert headers exist
        Assert.That(methodNameIndex, Is.GreaterThanOrEqualTo(0), "FullName column should exist");
        Assert.That(isStaticIndex, Is.GreaterThanOrEqualTo(0), "IsStatic column should exist");
        Assert.That(isExtensionIndex, Is.GreaterThanOrEqualTo(0), "IsExtension column should exist");

        // assert data
        Assert.That(lines[1].Trim(), Does.StartWith("Method1"), "First data line should be Method1");
        Assert.That(lines[2].Trim(), Does.StartWith("Method2"), "Second data line should be Method2");
        foreach (var line in lines)
        {
            Assert.That(line, Does.Not.Contain("Gap"), "Template should not contain Gap");
        }
    }

    [Test]
    public void SaveOverrides_DoesNotWriteDuplicates()
    {
        // Arrange
        var reportItems = new List<ReportItem>
        {
            new() { FullName = "Method1", Parameters = new Dictionary<string, string> { { "IsStatic", "True" }, { "CustomParam", "Value1" } } },
            new() { FullName = "Method1", Parameters = new Dictionary<string, string> { { "IsStatic", "True" }, { "CustomParam", "Value1" } } },
            new() { FullName = "Method2", Parameters = new Dictionary<string, string> { { "IsExtension", "True" }, { "CustomParam", "Value2" } } }
        };

        // Act
        _overrideManager.SaveOverrides(reportItems);

        // Assert
        var overrides = _csvUtils.ReadCsv(_overrideTemplateFilePath);
        Assert.That(overrides.Count, Is.EqualTo(2), "Should have only 2 entries (no duplicates)");
        Assert.That(overrides.Any(r => r["FullName"] == "Method1" && r["CustomParam"] == "Value1"), Is.True, "Should contain Method1");
        Assert.That(overrides.Any(r => r["FullName"] == "Method2" && r["CustomParam"] == "Value2"), Is.True, "Should contain Method2");
    }

    [Test]
    public void MethodOverrideManager_ShouldOnlyOverrideStaticParameters()
    {
        // Arrange
        var overrideContent = "FullName,StaticParam,DynamicParam\nTestClass.TestMethod,StaticOverride,DynamicOverride";
        _fileSystem.WriteAllText(_overrideFilePath, overrideContent);
        _overrideManager.ReadOverrides();

        var reportItem = new ReportItem
        {
            FullName = "TestClass.TestMethod",
            Parameters = new Dictionary<string, string>
            {
                { "StaticParam", "OriginalStatic" },
                { "DynamicParam", "OriginalDynamic" }
            },
            AttributeParameters = new HashSet<string> { "StaticParam" }
        };

        // Act
        var overrides = _overrideManager.GetOverridesForMethod(reportItem.FullName, _ => true);
        foreach (var kvp in overrides)
        {
            if (reportItem.IsStaticParameter(kvp.Key))
            {
                ((Dictionary<string, string>)reportItem.Parameters)[kvp.Key] = kvp.Value;
            }
        }

        // Assert
        Assert.That(reportItem.Parameters["StaticParam"], Is.EqualTo("StaticOverride"));
        Assert.That(reportItem.Parameters["DynamicParam"], Is.EqualTo("OriginalDynamic"));
    }
}

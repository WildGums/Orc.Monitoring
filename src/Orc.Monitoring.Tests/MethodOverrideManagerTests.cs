namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using Orc.Monitoring.Reporters.ReportOutputs;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
        var overrides = _overrideManager.GetOverridesForMethod(methodName);
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
    [TestCase("TestNamespace.TestClass.TestMethod", true, false, "CustomValue1")]
    [TestCase("TestNamespace.TestClass.AnotherMethod", false, true, "CustomValue2")]
    [TestCase("TestNamespace.TestClass.GenericMethod<T>", false, false, "CustomValue3")]
    public void SaveOverrides_CreatesTemplateFileWithCorrectColumns(string methodName, bool isStatic, bool isExtension, string customValue)
    {
        // Arrange
        var reportItems = new List<ReportItem>
        {
            new ReportItem
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
        var templateContent = _fileSystem.ReadAllText(_overrideTemplateFilePath);
        Assert.That(templateContent, Does.Contain("FullName,IsStatic,IsExtension"), "Template should contain standard columns");
        Assert.That(templateContent, Does.Contain($"{methodName},{isStatic},{isExtension}"), "Template should contain correct values");
        Assert.That(templateContent, Does.Not.Contain("CustomColumn"), "Template should not contain custom columns");
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
        var overrides = _overrideManager.GetOverridesForMethod(methodName);

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
                allOverrides.Add(_overrideManager.GetOverridesForMethod(parts[0]));
            }
        }
        Assert.That(allOverrides.Count, Is.EqualTo(expectedOverrideCount));
        Assert.That(allOverrides.All(o => o.Count > 0), Is.True);
    }
}

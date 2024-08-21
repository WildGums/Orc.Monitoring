#pragma warning disable CL0002
namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using Orc.Monitoring.Reporters;
using Orc.Monitoring.Reporters.ReportOutputs;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Orc.Monitoring.MethodLifeCycleItems;


[TestFixture]
public class RanttOutputTests
{
    private RanttOutput _ranttOutput;
    private MockReporter _mockReporter;
    private string _testFolderPath;

    [SetUp]
    public void Setup()
    {
        _testFolderPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_testFolderPath);
        _ranttOutput = new RanttOutput();
        _mockReporter = new MockReporter { Name = "TestReporter", FullName = "TestReporter" };
        var parameters = RanttOutput.CreateParameters(_testFolderPath);
        _ranttOutput.SetParameters(parameters);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_testFolderPath))
        {
            Directory.Delete(_testFolderPath, true);
        }
    }

    [Test]
    public async Task WriteItem_CorrectlyGeneratesRelationships()
    {
        // Arrange
        var disposable = _ranttOutput.Initialize(_mockReporter);

        // Create a parent method call
        var parentMethodInfo = CreateMethodCallInfo("ParentMethod", null);
        var parentStart = new MethodCallStart(parentMethodInfo);
        _ranttOutput.WriteItem(parentStart);

        // Create a child method call
        var childMethodInfo = CreateMethodCallInfo("ChildMethod", parentMethodInfo);
        var childStart = new MethodCallStart(childMethodInfo);
        _ranttOutput.WriteItem(childStart);

        // End both method calls
        var childEnd = new MethodCallEnd(childMethodInfo);
        _ranttOutput.WriteItem(childEnd);
        var parentEnd = new MethodCallEnd(parentMethodInfo);
        _ranttOutput.WriteItem(parentEnd);

        // Act
        await disposable.DisposeAsync();

        // Assert
        var relationshipsFilePath = Path.Combine(_testFolderPath, "TestReporter", "TestReporter_Relationships.csv");
        Assert.That(File.Exists(relationshipsFilePath), Is.True, "Relationships file should exist");

        var relationshipsContent = await File.ReadAllTextAsync(relationshipsFilePath);
        Console.WriteLine($"Relationships file content:\n{relationshipsContent}");

        var lines = relationshipsContent.Split('\n');
        Assert.That(lines.Length, Is.GreaterThan(1), "Relationships file should have more than just the header");
        Assert.That(lines[1].StartsWith(parentMethodInfo.Id + "," + childMethodInfo.Id), Is.True, "Relationship between parent and child should be present");
    }

    private MethodCallInfo CreateMethodCallInfo(string methodName, MethodCallInfo parent)
    {
        var methodInfo = new TestMethodInfo(methodName, typeof(RanttOutputTests));
        var methodCallInfo = MethodCallInfo.Create(
            new MethodCallInfoPool(),
            null,
            typeof(RanttOutputTests),
            methodInfo,
            Array.Empty<Type>(),
            Guid.NewGuid().ToString(),
            new System.Collections.Generic.Dictionary<string, string>()
        );
        methodCallInfo.Parent = parent;
        return methodCallInfo;
    }
}

namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using Orc.Monitoring.Reporters.ReportOutputs;
using Orc.Monitoring.Reporters;
using Orc.Monitoring.MethodLifeCycleItems;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Reflection;
#pragma warning disable CL0002


[TestFixture]
public class MethodOverrideManagerAndRanttOutputTests
{
    private string _testOutputPath;

    [SetUp]
    public void Setup()
    {
        _testOutputPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_testOutputPath);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_testOutputPath))
        {
            Directory.Delete(_testOutputPath, true);
        }
    }

    public class DuplicateKeyDictionary<TKey, TValue> : Dictionary<TKey, TValue>
    {
        private readonly List<KeyValuePair<TKey, TValue>> _items = new List<KeyValuePair<TKey, TValue>>();

        public new void Add(TKey key, TValue value)
        {
            _items.Add(new KeyValuePair<TKey, TValue>(key, value));
            base[key] = value; // This will overwrite if the key already exists
        }

        public new IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return _items.GetEnumerator();
        }
    }

    [Test]
    public void SaveOverrides_WithDuplicateCustomColumns_ShouldNotProduceDuplicateColumnsInCsv()
    {
        var manager = new MethodOverrideManager(_testOutputPath);
        var parameters = new Dictionary<string, string>()
        {
            { "CustomColumn", "Value1" },
            { "customcolumn", "Value2" } // This will overwrite the previous value due to case-insensitive dictionary
        };

        var reportItems = new List<ReportItem>
        {
            new ReportItem
            {
                FullName = "Test.Method",
                Parameters = parameters,
                AttributeParameters = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "CustomColumn" }
            }
        };

        manager.SaveOverrides(reportItems);

        var csvContent = File.ReadAllText(Path.Combine(_testOutputPath, "method_overrides.csv"));
        Console.WriteLine($"CSV Content:\n{csvContent}");
        var headers = csvContent.Split('\n')[0].Split(',').Select(h => h.Trim()).ToArray();
        Console.WriteLine($"Headers: {string.Join(", ", headers)}");
        var uniqueHeaders = new HashSet<string>(headers, StringComparer.OrdinalIgnoreCase);
        Console.WriteLine($"Unique Headers: {string.Join(", ", uniqueHeaders)}");

        Assert.That(headers.Length, Is.EqualTo(uniqueHeaders.Count), "CSV should not contain duplicate columns");
        Assert.That(uniqueHeaders, Does.Contain("CustomColumn"), "CustomColumn should be present");
    }

    [Test]
    public void SaveOverrides_MultipleSaves_ShouldNotDuplicateColumns()
    {
        var manager = new MethodOverrideManager(_testOutputPath);
        var reportItems1 = new List<ReportItem>
        {
            new ReportItem
            {
                FullName = "Test.Method1",
                Parameters = new Dictionary<string, string> { { "CustomColumn1", "Value1" } },
                AttributeParameters = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "CustomColumn1" }
            }
        };

        var reportItems2 = new List<ReportItem>
        {
            new ReportItem
            {
                FullName = "Test.Method2",
                Parameters = new Dictionary<string, string> { { "CustomColumn2", "Value2" } },
                AttributeParameters = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "CustomColumn2" }
            }
        };

        manager.SaveOverrides(reportItems1);
        manager.SaveOverrides(reportItems2);

        var csvContent = File.ReadAllText(Path.Combine(_testOutputPath, "method_overrides.csv"));
        Console.WriteLine($"CSV Content:\n{csvContent}");
        var headers = csvContent.Split('\n')[0].Split(',').Select(h => h.Trim()).ToArray();
        Console.WriteLine($"Headers: {string.Join(", ", headers)}");
        var uniqueHeaders = new HashSet<string>(headers, StringComparer.OrdinalIgnoreCase);
        Console.WriteLine($"Unique Headers: {string.Join(", ", uniqueHeaders)}");

        Assert.That(headers.Length, Is.EqualTo(uniqueHeaders.Count), "CSV should not contain duplicate columns after multiple saves");
        Assert.That(uniqueHeaders, Does.Contain("CustomColumn1"), "CustomColumn1 should be present");
        Assert.That(uniqueHeaders, Does.Contain("CustomColumn2"), "CustomColumn2 should be present");
    }

    [Test]
    public void SaveOverrides_ConcurrentAccess_ShouldNotProduceDuplicateColumns()
    {
        var manager = new MethodOverrideManager(_testOutputPath);
        var tasks = new List<Task>();

        for (int i = 0; i < 10; i++)
        {
            var i1 = i;
            tasks.Add(Task.Run(() =>
            {
                var reportItems = new List<ReportItem>
                {
                    new ReportItem
                    {
                        FullName = $"Test.Method{i1}",
                        Parameters = new Dictionary<string, string> { { $"CustomColumn{i1}", $"Value{i1}" } },
                        AttributeParameters = new HashSet<string> { $"CustomColumn{i1}" }
                    }
                };
                manager.SaveOverrides(reportItems);
            }));
        }

        Task.WaitAll(tasks.ToArray());

        var csvContent = File.ReadAllText(Path.Combine(_testOutputPath, "method_overrides.csv"));
        var headers = csvContent.Split('\n')[0].Split(',');
        var uniqueHeaders = new HashSet<string>(headers);

        Assert.That(headers.Length, Is.EqualTo(uniqueHeaders.Count), "CSV should not contain duplicate columns after concurrent saves");
    }

    [Test]
    public async Task RanttOutput_GenerateReport_ShouldProduceValidRanttFile()
    {
        var ranttOutput = new RanttOutput();
        var parameters = RanttOutput.CreateParameters(_testOutputPath);
        ranttOutput.SetParameters(parameters);

        var mockReporter = new Mock<IMethodCallReporter>();
        mockReporter.Setup(r => r.FullName).Returns("TestReporter");

        await using (var disposable = ranttOutput.Initialize(mockReporter.Object))
        {
            // Simulate writing items
            for (int i = 0; i < 100; i++)
            {
                var item = CreateTestMethodLifeCycleItem($"Item{i}", DateTime.Now.AddMinutes(-i));
                ranttOutput.WriteItem(item);
            }
        }

        var ranttProjectFile = Path.Combine(_testOutputPath, "TestReporter", "TestReporter.rprjx");
        Assert.That(File.Exists(ranttProjectFile), Is.True, "Rantt project file should be created");

        // Verify Rantt file integrity
        VerifyRanttFileIntegrity(ranttProjectFile);
    }

    private ICallStackItem CreateTestMethodLifeCycleItem(string itemName, DateTime timestamp)
    {
        var methodInfo = new TestMethodInfo(itemName, typeof(MethodOverrideManagerAndRanttOutputTests));
        var methodCallInfo = MethodCallInfo.Create(
            new MethodCallInfoPool(),
            null,
            typeof(MethodOverrideManagerAndRanttOutputTests),
            methodInfo,
            Array.Empty<Type>(),
            Guid.NewGuid().ToString(),
            new Dictionary<string, string>()
        );
        methodCallInfo.StartTime = timestamp;

        var mockMethodLifeCycleItem = new Mock<MethodCallStart>(methodCallInfo);
        return mockMethodLifeCycleItem.Object;
    }

    private void VerifyRanttFileIntegrity(string ranttProjectFile)
    {
        // Read the Rantt project file
        var projectContent = File.ReadAllText(ranttProjectFile);

        // Perform basic checks on the file content
        Assert.That(projectContent, Does.Contain("<Project RanttVersion="), "Rantt project file should contain version information");
        Assert.That(projectContent, Does.Contain("<DataSets>"), "Rantt project file should contain DataSets section");
        Assert.That(projectContent, Does.Contain("<Operations"), "Rantt project file should contain Operations section");
        Assert.That(projectContent, Does.Contain("<Relationships"), "Rantt project file should contain Relationships section");

        // Verify referenced CSV files exist
        var csvFileName = projectContent.Split(new[] { "Source=\"" }, StringSplitOptions.None)[1].Split('"')[0];
        var csvFilePath = Path.Combine(Path.GetDirectoryName(ranttProjectFile), csvFileName);
        Assert.That(File.Exists(csvFilePath), Is.True, "Referenced CSV file should exist");

        // Check CSV file content
        var csvContent = File.ReadAllText(csvFilePath);
        var csvLines = csvContent.Split('\n');
        Assert.That(csvLines.Length, Is.GreaterThan(1), "CSV file should contain data");

        var headers = csvLines[0].Split(',');
        Assert.That(headers, Does.Contain("MethodName"), "CSV should contain MethodName column");
        Assert.That(headers, Does.Contain("Id"), "CSV should contain Id column");

        // Check for duplicate columns in CSV
        var uniqueHeaders = new HashSet<string>(headers);
        Assert.That(headers.Length, Is.EqualTo(uniqueHeaders.Count), "CSV should not contain duplicate columns");
    }
}

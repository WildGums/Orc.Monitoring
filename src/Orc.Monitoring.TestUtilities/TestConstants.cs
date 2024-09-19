namespace Orc.Monitoring.TestUtilities;

using System;

public static class TestConstants
{
    // Common string constants
    public const string DefaultTestReporterName = "TestReporter";
    public const string DefaultTestMethodName = "TestMethod";
    public const string DefaultTestClassName = "TestClass";
    public const string DefaultTestWorkflowItemName = "TestWorkflowItem";
    public const string DefaultTestParameterName = "TestParam";
    public const string DefaultTestParameterValue = "TestValue";

    // File and path related constants
    public const string DefaultTestOutputPath = "TestOutput";
    public const string DefaultTestFileName = "TestFile";
    public const string DefaultCsvFileName = "TestReport.csv";
    public const string DefaultTxtFileName = "TestReport.txt";
    public const string DefaultRanttFileName = "TestReport.rprjx";
    public const string DefaultOverrideFileName = "method_overrides.csv";
    public const string DefaultTemplateFileName = "method_overrides.template";

    // Numeric constants
    public const int DefaultTestThreadId = 1;
    public const int DefaultTestParentThreadId = 0;
    public const int DefaultTestLevel = 1;
    public const int DefaultTestDuration = 1000; // milliseconds
    public const int DefaultTestMaxItems = 100;
    public const int DefaultTestConcurrency = 10;

    // DateTime related constants
    public static readonly DateTime DefaultTestStartTime = new DateTime(2023, 1, 1, 0, 0, 0);
    public static readonly TimeSpan DefaultTestElapsedTime = TimeSpan.FromSeconds(1);

    // GUID related constants
    public static readonly Guid DefaultTestGuid = new Guid("11111111-1111-1111-1111-111111111111");

    // Method parameter related constants
    public const string WorkflowItemNameParameter = "WorkflowItemName";
    public const string WorkflowItemTypeParameter = "WorkflowItemType";

    // Test data related constants
    public const string TestCsvContent = "Header1,Header2,Header3\nValue1,Value2,Value3\nValue4,Value5,Value6";
    public const string TestJsonContent = "{\"key\":\"value\"}";

    // Test configuration related constants
    public const bool DefaultIsEnabled = true;
    public const bool DefaultIsReporterEnabled = true;
    public const bool DefaultIsFilterEnabled = true;

    // Monitoring version related constants
    public const long DefaultVersionTimestamp = 100;
    public const int DefaultVersionCounter = 0;

    // Test method signature related constants
    public static readonly Type[] EmptyTypeArray = Array.Empty<Type>();
    public static readonly object[] EmptyObjectArray = Array.Empty<object>();

    // File content related constants
    public const string CsvHeaderLine = "Id,ParentId,StartTime,EndTime,MethodName,Duration";
    public const string TxtHeaderLine = "---- Test Report ----";

    // Test exception messages
    public const string DefaultTestExceptionMessage = "Test exception";

    // Configuration related constants
    public const string DefaultConfigurationName = "TestConfiguration";

    // Filter related constants
    public const string DefaultFilterName = "TestFilter";

    // Performance monitoring related constants
    public const string DefaultPerformanceMetricName = "TestMetric";
    public const double DefaultPerformanceMetricValue = 100.0;

    // Async operation related constants
    public const int DefaultAsyncOperationDelay = 100; // milliseconds

    // Reporting related constants
    public const string DefaultReportName = "TestReport";
    public const string DefaultReportSummary = "Test Summary";
}

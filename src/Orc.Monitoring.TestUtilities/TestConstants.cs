namespace Orc.Monitoring.TestUtilities;

using System;

public static class TestConstants
{
    // Test item related constants
    public const string DefaultCsvReportFileName = "TestReport.csv";
    public const int DefaultItemCount = 10;
    public static readonly DateTime DefaultItemStartTime = new DateTime(2023, 1, 1, 0, 0, 0);

    // Common string constants
    public const string DefaultTestReporterName = "TestReporter";
    public const string DefaultTestMethodName = "TestMethod";

    // File and path related constants
    public const string DefaultCsvFileName = "TestReport.csv";
    public const string DefaultTxtFileName = "TestReport.txt";
    public const string DefaultRanttFileName = "TestReport.rprjx";
    public const string DefaultOverrideFileName = "method_overrides.csv";
    public const string DefaultTemplateFileName = "method_overrides.template";

    // Numeric constants
    public const int DefaultTestMaxItems = 100;

    // GUID related constants
    public static readonly Guid DefaultTestGuid = new Guid("11111111-1111-1111-1111-111111111111");

    // Test configuration related constants
    public const bool DefaultIsEnabled = true;

    // Monitoring version related constants
    public const long DefaultVersionTimestamp = 100;
    public const int DefaultVersionCounter = 0;

    // Test method signature related constants
    public static readonly Type[] EmptyTypeArray = Array.Empty<Type>();
    public static readonly object[] EmptyObjectArray = Array.Empty<object>();

    // File content related constants
    public const string CsvHeaderLine = "Id,ParentId,StartTime,EndTime,Report,ClassName,MethodName,FullName,Duration,ThreadId,ParentThreadId,NestingLevel,IsStatic,IsGeneric,IsExtension";

    // Async operation related constants
    public const int DefaultAsyncOperationDelay = 100; // milliseconds

    // Reporting related constants
    public const string DefaultReportName = "TestReport";
}

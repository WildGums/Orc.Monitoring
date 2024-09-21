namespace Orc.Monitoring.TestUtilities
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Threading.Tasks;
    using Core.Abstractions;
    using Core.Attributes;
    using Core.CallStacks;
    using Core.Configuration;
    using Core.IO;
    using Core.MethodCallContexts;
    using Core.MethodLifecycle;
    using Core.Models;
    using Core.PerformanceMonitoring;
    using Core.Pooling;
    using Core.Utilities;
    using Moq;
    using NUnit.Framework;
    using Monitoring;
    using Reporters;
    using Reporters.ReportOutputs;
    using Filters;
    using Mocks;
    using TestHelpers;

    public static class TestHelperMethods
    {
        public static MethodCallInfo CreateMethodCallInfo(
            IClassMonitor classMonitor,
            CallStack callStack,
            string methodName,
            Type declaringType,
            MethodCallInfoPool methodCallInfoPool)
        {
            var config = new MethodCallContextConfig
            {
                ClassType = declaringType,
                CallerMethodName = methodName
            };

            var testMethod = new TestMethodInfo(methodName, declaringType);
            testMethod.SetCustomAttribute(new MethodCallParameterAttribute("TestParam", "TestValue"));

            return callStack.CreateMethodCallInfo(classMonitor, declaringType, config, testMethod);
        }

        public static Mock<IClassMonitor> CreateMockClassMonitor()
        {
            return new Mock<IClassMonitor>();
        }

        public static Mock<IMonitoringController> CreateMockMonitoringController(bool isEnabled = true)
        {
            var mock = new Mock<IMonitoringController>();
            mock.Setup(c => c.IsEnabled).Returns(isEnabled);
            mock.Setup(c => c.GetCurrentVersion()).Returns(new MonitoringVersion(1, 0, Guid.NewGuid()));
            return mock;
        }

        public static Mock<IMethodCallReporter> CreateMockReporter(string name = "TestReporter")
        {
            var mock = new Mock<IMethodCallReporter>();
            mock.Setup(r => r.Name).Returns(name);
            mock.Setup(r => r.FullName).Returns(name);
            return mock;
        }

        public static CallStack CreateTestCallStack(
            IMonitoringController monitoringController,
            MonitoringConfiguration config,
            MethodCallInfoPool methodCallInfoPool,
            IMonitoringLoggerFactory loggerFactory)
        {
            return new CallStack(monitoringController, config, methodCallInfoPool, loggerFactory);
        }

        public static MethodCallInfo CreateExternalMethodCallInfo(
            IClassMonitor classMonitor,
            CallStack callStack,
            string methodName,
            Type externalType,
            MethodCallInfoPool methodCallInfoPool)
        {
            var config = new MethodCallContextConfig
            {
                CallerMethodName = methodName
            };

            var methodInfo = new TestMethodInfo(methodName, externalType);

            return callStack.CreateMethodCallInfo(classMonitor, externalType, config, methodInfo, true);
        }

        public static ICallStackItem CreateTestMethodLifeCycleItem(
            string itemName,
            DateTime timestamp,
            MethodCallInfoPool methodCallInfoPool)
        {
            var methodInfo = new TestMethodInfo(itemName, typeof(TestHelperMethods));
            var methodCallInfo = methodCallInfoPool.Rent(
                null,
                typeof(TestHelperMethods),
                methodInfo,
                Array.Empty<Type>(),
                Guid.NewGuid().ToString(),
                new Dictionary<string, string>()
            );
            methodCallInfo.StartTime = timestamp;

            return new MethodCallStart(methodCallInfo);
        }

        public static MonitoringConfiguration CreateTestConfiguration()
        {
            return new MonitoringConfiguration();
        }

        public static AlwaysIncludeFilter CreateAlwaysIncludeFilter(IMonitoringLoggerFactory loggerFactory)
        {
            return new AlwaysIncludeFilter(loggerFactory);
        }

        public static MethodCallInfoPool CreateMethodCallInfoPool(
            IMonitoringController monitoringController,
            IMonitoringLoggerFactory loggerFactory)
        {
            return new MethodCallInfoPool(monitoringController, loggerFactory);
        }

        public static MethodCallContextFactory CreateMethodCallContextFactory(
            IMonitoringController monitoringController,
            IMonitoringLoggerFactory loggerFactory,
            MethodCallInfoPool methodCallInfoPool)
        {
            return new MethodCallContextFactory(monitoringController, loggerFactory, methodCallInfoPool);
        }

        public static IPerformanceMonitor CreateTestPerformanceMonitor(
            IMonitoringController monitoringController,
            IMonitoringLoggerFactory loggerFactory,
            ICallStackFactory callStackFactory,
            IClassMonitorFactory classMonitorFactory)
        {
            return new PerformanceMonitor(monitoringController, loggerFactory,
                callStackFactory,
                classMonitorFactory,
                () => new ConfigurationBuilder(monitoringController));
        }

        public static IAsyncDisposable CreateTestReportOutput<T>(
            T reportOutput,
            string testOutputPath,
            IMethodCallReporter reporter) where T : IReportOutput
        {
            var parameters = typeof(T).GetMethod("CreateParameters")?.Invoke(null, new object[] { testOutputPath });
            reportOutput.SetParameters(parameters);
            return reportOutput.Initialize(reporter);
        }

        public static InMemoryFileSystem CreateInMemoryFileSystem(IMonitoringLoggerFactory loggerFactory)
        {
            return new InMemoryFileSystem(loggerFactory);
        }

        public static ReportArchiver CreateReportArchiver(IFileSystem fileSystem, IMonitoringLoggerFactory loggerFactory)
        {
            return new ReportArchiver(fileSystem, loggerFactory);
        }

        public static CsvUtils CreateCsvUtils(IFileSystem fileSystem, IMonitoringLoggerFactory loggerFactory)
        {
            return new CsvUtils(fileSystem, loggerFactory);
        }

        public static void SetupMockReporter(Mock<IMethodCallReporter> mockReporter, string name = "TestReporter")
        {
            mockReporter.Setup(r => r.Name).Returns(name);
            mockReporter.Setup(r => r.FullName).Returns(name);
            mockReporter.Setup(r => r.RootMethod).Returns((MethodInfo)null);
        }

        public static ReportItem CreateTestReportItem(string id, string methodName, string parentId = null)
        {
            return new ReportItem
            {
                Id = id,
                MethodName = methodName,
                Parent = parentId,
                StartTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                EndTime = DateTime.Now.AddSeconds(1).ToString("yyyy-MM-dd HH:mm:ss.fff"),
                FullName = $"TestClass.{methodName}()",
                ClassName = "TestClass",
                ThreadId = "1",
                ParentThreadId = parentId is null ? "0" : "1",
                Report = "TestReport"
            };
        }

        public static void AssertFileExists(IFileSystem fileSystem, string filePath)
        {
            Assert.That(fileSystem.FileExists(filePath), Is.True, $"{filePath} should exist");
        }

        public static async Task<string> ReadFileContentAsync(IFileSystem fileSystem, string filePath)
        {
            AssertFileExists(fileSystem, filePath);
            return await fileSystem.ReadAllTextAsync(filePath);
        }
    }
}

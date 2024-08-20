﻿// ReSharper disable InconsistentNaming
namespace Orc.Monitoring.Reporters;

using System;
using System.Reflection;
using MethodLifeCycleItems;

/// <summary>
/// Defines a method for reporting performance data.
/// </summary>
public interface IMethodCallReporter : IOutputContainer
{
    string Name { get; }
    string FullName { get; }
    MethodInfo? RootMethod { get; set; }

    IAsyncDisposable StartReporting(IObservable<ICallStackItem> callStack);
}

public static class MethodCallParameter
{
    public const string WorkflowItemName = "WorkflowItemName";
    public const string WorkflowItemType = "WorkflowItemType";
    public const string WorkflowItemLevel = "WorkflowItemLevel";
    public const string SqlQuery = "SqlQuery";
    public const string EntityName = "EntityName";
    public const string Result = "Result";
    public const string Input = "Input";

    public static class Types
    {
        public const string Gap = "Gap";
        public const string UserInteraction = "UserInteraction";
        public const string DataProcess = "DataProcess";
        public const string DataIo = "DataIO";
        public const string Refresh = "Refresh";
        public const string Overview = "Overview";
    }

    public static class Levels
    {
        public const string High = "High";
        public const string Medium = "Medium";
        public const string Low = "Low";
    }
}

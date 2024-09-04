namespace Orc.Monitoring.Reporters;

public static class MethodCallParameter
{
    public const string WorkflowItemName = "WorkflowItemName";
    public const string WorkflowItemType = "WorkflowItemType";
    public const string WorkflowItemGranularity = "WorkflowItemGranularity";
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

    public static class Granularity
    {
        public const string Coarse = "Coarse";
        public const string Medium = "Medium";
        public const string Fine = "Fine";
    }
}

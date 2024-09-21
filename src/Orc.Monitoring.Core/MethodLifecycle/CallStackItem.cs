namespace Orc.Monitoring.Core.MethodLifecycle;
public static class CallStackItem
{
    public static ICallStackItem Empty { get; } = new EmptyCallStackItem();

    private class EmptyCallStackItem : ICallStackItem
    {
        public override string ToString() => "EmptyCallStackItem";
    }
}

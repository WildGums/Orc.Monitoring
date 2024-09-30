namespace Orc.Monitoring.Core.Abstractions;

using CallStacks;
using Configuration;

public interface ICallStackFactory
{
    CallStack CreateCallStack();
}

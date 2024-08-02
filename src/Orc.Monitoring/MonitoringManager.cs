// ReSharper disable InconsistentNaming
namespace Orc.Monitoring;

using System;
using System.Threading;


public static class MonitoringManager
{
    private static int _isEnabled = 0;
    private static int _version = 0;
    private static int _activeOperations = 0;
    private static event Action<bool, int>? StateChanged;

    public static bool IsEnabled => Interlocked.CompareExchange(ref _isEnabled, 0, 0) == 1;

    public static int CurrentVersion => Interlocked.CompareExchange(ref _version, 0, 0);

    public static void Enable()
    {
        if (Interlocked.Exchange(ref _isEnabled, 1) == 0)
        {
            int newVersion = Interlocked.Increment(ref _version);
            StateChanged?.Invoke(true, newVersion);
        }
    }

    public static void Disable()
    {
        if (Interlocked.Exchange(ref _isEnabled, 0) == 1)
        {
            int newVersion = Interlocked.Increment(ref _version);
            StateChanged?.Invoke(false, newVersion);
        }
    }

    public static IDisposable BeginOperation()
    {
        Interlocked.Increment(ref _activeOperations);
        return new OperationScope();
    }

    private sealed class OperationScope : IDisposable
    {
        public void Dispose()
        {
            Interlocked.Decrement(ref _activeOperations);
        }
    }

    public static bool ShouldTrack(int operationVersion)
    {
        return IsEnabled || (Interlocked.CompareExchange(ref _activeOperations, 0, 0) > 0 && operationVersion == CurrentVersion);
    }

    public static void AddStateChangedCallback(Action<bool, int> callback)
    {
        StateChanged += callback;
    }

    public static int GetCurrentVersion()
    {
        return Interlocked.CompareExchange(ref _version, 0, 0);
    }
}

// ReSharper disable InconsistentNaming
#pragma warning disable IDE1006
namespace Orc.Monitoring;

using System;
using System.Collections.Concurrent;
using System.Threading;
using Microsoft.Extensions.Logging;


public static class MonitoringManager
{
    private static int _isEnabled = 0;
    private static int _version = 0;
    private static int _activeOperations = 0;
    private static readonly ConcurrentDictionary<Type, bool> _reporterStates = new();
    private static readonly ConcurrentDictionary<Type, bool> _filterStates = new();
    private static readonly ReaderWriterLockSlim _stateLock = new();

    private static readonly ILoggerFactory LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
    {
        builder.AddConsole();
    });

    private static readonly ILogger _logger = LoggerFactory.CreateLogger(typeof(MonitoringManager));

    public enum MonitoringComponentType
    {
        Global,
        Reporter,
        Filter
    }

    public static event Action<MonitoringComponentType, string, bool, int>? StateChanged;

    public static bool IsEnabled => Interlocked.CompareExchange(ref _isEnabled, 0, 0) == 1;

    public static int CurrentVersion => Interlocked.CompareExchange(ref _version, 0, 0);

    /// <summary>
    /// Enables the global monitoring.
    /// </summary>
    public static void Enable()
    {
        _stateLock.EnterWriteLock();
        try
        {
            if (Interlocked.Exchange(ref _isEnabled, 1) == 0)
            {
                int newVersion = Interlocked.Increment(ref _version);
                _logger.LogDebug($"Monitoring enabled. New version: {newVersion}");
                StateChanged?.Invoke(MonitoringComponentType.Global, "Global", true, newVersion);
            }
        }
        finally
        {
            _stateLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Disables the global monitoring.
    /// </summary>
    public static void Disable()
    {
        _stateLock.EnterWriteLock();
        try
        {
            if (Interlocked.Exchange(ref _isEnabled, 0) == 1)
            {
                int newVersion = Interlocked.Increment(ref _version);
                _logger.LogDebug($"Monitoring disabled. New version: {newVersion}");
                StateChanged?.Invoke(MonitoringComponentType.Global, "Global", false, newVersion);
            }
        }
        finally
        {
            _stateLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Enables monitoring for the specified reporter type.
    /// </summary>
    /// <param name="reporterType">The type of the reporter to enable.</param>
    public static void EnableReporter(Type reporterType)
    {
        EnableComponent(MonitoringComponentType.Reporter, _reporterStates, reporterType);
    }

    /// <summary>
    /// Disables monitoring for the specified reporter type.
    /// </summary>
    /// <param name="reporterType">The type of the reporter to disable.</param>
    public static void DisableReporter(Type reporterType)
    {
        DisableComponent(MonitoringComponentType.Reporter, _reporterStates, reporterType);
    }

    /// <summary>
    /// Checks if the specified reporter type is enabled.
    /// </summary>
    /// <param name="reporterType">The type of the reporter to check.</param>
    /// <returns>True if the reporter is enabled, false otherwise.</returns>
    public static bool IsReporterEnabled(Type reporterType)
    {
        return IsComponentEnabled(_reporterStates, reporterType);
    }

    /// <summary>
    /// Enables monitoring for the specified filter type.
    /// </summary>
    /// <param name="filterType">The type of the filter to enable.</param>
    public static void EnableFilter(Type filterType)
    {
        EnableComponent(MonitoringComponentType.Filter, _filterStates, filterType);
    }

    /// <summary>
    /// Disables monitoring for the specified filter type.
    /// </summary>
    /// <param name="filterType">The type of the filter to disable.</param>
    public static void DisableFilter(Type filterType)
    {
        DisableComponent(MonitoringComponentType.Filter, _filterStates, filterType);
    }

    /// <summary>
    /// Checks if the specified filter type is enabled.
    /// </summary>
    /// <param name="filterType">The type of the filter to check.</param>
    /// <returns>True if the filter is enabled, false otherwise.</returns>
    public static bool IsFilterEnabled(Type filterType)
    {
        return IsComponentEnabled(_filterStates, filterType);
    }

    /// <summary>
    /// Temporarily enables a reporter for the duration of the returned IDisposable.
    /// </summary>
    /// <param name="reporterType">The type of the reporter to temporarily enable.</param>
    /// <returns>An IDisposable that, when disposed, reverts the reporter to its previous state.</returns>
    public static IDisposable TemporarilyEnableReporter(Type reporterType)
    {
        return new TemporaryStateChange(MonitoringComponentType.Reporter, _reporterStates, reporterType);
    }

    /// <summary>
    /// Temporarily enables a filter for the duration of the returned IDisposable.
    /// </summary>
    /// <param name="filterType">The type of the filter to temporarily enable.</param>
    /// <returns>An IDisposable that, when disposed, reverts the filter to its previous state.</returns>
    public static IDisposable TemporarilyEnableFilter(Type filterType)
    {
        return new TemporaryStateChange(MonitoringComponentType.Filter, _filterStates, filterType);
    }

    private static void EnableComponent(MonitoringComponentType componentType, ConcurrentDictionary<Type, bool> statesDictionary, Type type)
    {
        _stateLock.EnterWriteLock();
        try
        {
            bool wasEnabled = statesDictionary.TryGetValue(type, out bool currentState) && currentState;
            if (!wasEnabled)
            {
                statesDictionary[type] = true;
                int newVersion = Interlocked.Increment(ref _version);
                _logger.LogDebug($"{componentType} {type.Name} enabled. New version: {newVersion}");
                StateChanged?.Invoke(componentType, type.Name, true, newVersion);
            }
        }
        finally
        {
            _stateLock.ExitWriteLock();
        }
    }

    private static void DisableComponent(MonitoringComponentType componentType, ConcurrentDictionary<Type, bool> statesDictionary, Type type)
    {
        _stateLock.EnterWriteLock();
        try
        {
            bool wasDisabled = !statesDictionary.TryGetValue(type, out bool currentState) || !currentState;
            if (!wasDisabled)
            {
                statesDictionary[type] = false;
                int newVersion = Interlocked.Increment(ref _version);
                _logger.LogDebug($"{componentType} {type.Name} disabled. New version: {newVersion}");
                StateChanged?.Invoke(componentType, type.Name, false, newVersion);
            }
        }
        finally
        {
            _stateLock.ExitWriteLock();
        }
    }

    private static bool IsComponentEnabled(ConcurrentDictionary<Type, bool> statesDictionary, Type type)
    {
        return statesDictionary.TryGetValue(type, out bool isEnabled) && isEnabled;
    }

    public static IDisposable BeginOperation()
    {
        Interlocked.Increment(ref _activeOperations);
        return new OperationScope();
    }

    public static bool ShouldTrack(int operationVersion, Type? reporterType = null, Type? filterType = null)
    {
        if (!IsEnabled)
        {
            return Interlocked.CompareExchange(ref _activeOperations, 0, 0) > 0 && operationVersion == CurrentVersion;
        }

        if (reporterType is not null && !IsComponentEnabled(_reporterStates, reporterType))
        {
            return false;
        }

        if (filterType is not null && !IsComponentEnabled(_filterStates, filterType))
        {
            return false;
        }

        return true;
    }

    public static void AddStateChangedCallback(Action<MonitoringComponentType, string, bool, int> callback)
    {
        StateChanged += callback;
    }

    public static int GetCurrentVersion()
    {
        return Interlocked.CompareExchange(ref _version, 0, 0);
    }

    public static ILogger<T> CreateLogger<T>() => LoggerFactory.CreateLogger<T>();

    internal static void ResetForTesting()
    {
        _isEnabled = 0;
        _version = 0;
        _activeOperations = 0;
        _reporterStates.Clear();
        _filterStates.Clear();
    }

    private sealed class OperationScope : IDisposable
    {
        public void Dispose()
        {
            Interlocked.Decrement(ref _activeOperations);
        }
    }

    private sealed class TemporaryStateChange : IDisposable
    {
        private readonly MonitoringComponentType _componentType;
        private readonly ConcurrentDictionary<Type, bool> _statesDictionary;
        private readonly Type _type;
        private readonly bool _originalState;

        public TemporaryStateChange(MonitoringComponentType componentType, ConcurrentDictionary<Type, bool> statesDictionary, Type type)
        {
            _componentType = componentType;
            _statesDictionary = statesDictionary;
            _type = type;
            _originalState = IsComponentEnabled(statesDictionary, type);

            EnableComponent(componentType, statesDictionary, type);
        }

        public void Dispose()
        {
            if (!_originalState)
            {
                DisableComponent(_componentType, _statesDictionary, _type);
            }
        }
    }
}

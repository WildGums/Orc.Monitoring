// ReSharper disable InconsistentNaming
#pragma warning disable IDE1006
namespace Orc.Monitoring;

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Reflection;
using Microsoft.Extensions.Logging;

public static class MonitoringController
{
    private static int _isEnabled = 0;
    private static int _version = 0;
    private static int _activeOperations = 0;
    private static readonly ConcurrentDictionary<Type, bool> _reporterStates = new();
    private static readonly ConcurrentDictionary<Type, bool> _filterStates = new();
    private static readonly ReaderWriterLockSlim _stateLock = new();

    private static readonly ILoggerFactory _loggerFactory = new LoggerFactory();

    private static readonly ILogger _logger = CreateLogger(typeof(MonitoringController));

    private static MonitoringConfiguration _configuration = new MonitoringConfiguration();

    public static ILogger<T> CreateLogger<T>()
    {
        return _loggerFactory.CreateLogger<T>();
    }

    public static ILogger CreateLogger(Type type)
    {
        return _loggerFactory.CreateLogger(type);
    }

    public static MonitoringConfiguration Configuration
    {
        get => _configuration;
        set => _configuration = value;
    }

    public enum MonitoringComponentType
    {
        Global,
        Reporter,
        Filter
    }

    public static event Action<MonitoringComponentType, string, bool, int>? StateChanged;

    public static bool IsEnabled => Interlocked.CompareExchange(ref _isEnabled, 0, 0) == 1;

    public static int CurrentVersion => Interlocked.CompareExchange(ref _version, 0, 0);

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

    public static void EnableReporter(Type reporterType)
    {
        EnableComponent(MonitoringComponentType.Reporter, _reporterStates, reporterType);
    }

    public static void DisableReporter(Type reporterType)
    {
        DisableComponent(MonitoringComponentType.Reporter, _reporterStates, reporterType);
    }

    public static bool IsReporterEnabled(Type reporterType)
    {
        return IsComponentEnabled(_reporterStates, reporterType);
    }

    public static void EnableFilter(Type filterType)
    {
        EnableComponent(MonitoringComponentType.Filter, _filterStates, filterType);
    }

    public static void DisableFilter(Type filterType)
    {
        DisableComponent(MonitoringComponentType.Filter, _filterStates, filterType);
    }

    public static bool IsFilterEnabled(Type filterType)
    {
        return IsComponentEnabled(_filterStates, filterType);
    }

    public static IDisposable TemporarilyEnableReporter(Type reporterType)
    {
        return new TemporaryStateChange(MonitoringComponentType.Reporter, _reporterStates, reporterType);
    }

    public static IDisposable TemporarilyEnableFilter(Type filterType)
    {
        return new TemporaryStateChange(MonitoringComponentType.Filter, _filterStates, filterType);
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

    public static bool ShouldMonitor(MethodInfo method)
    {
        return IsEnabled && _configuration.ShouldMonitor(method);
    }

    public static bool ShouldMonitor(Type type)
    {
        return IsEnabled && _configuration.ShouldMonitor(type);
    }

    public static bool ShouldMonitor(string @namespace)
    {
        return IsEnabled && _configuration.ShouldMonitor(@namespace);
    }

    public static void AddStateChangedCallback(Action<MonitoringComponentType, string, bool, int> callback)
    {
        StateChanged += callback;
    }

    public static int GetCurrentVersion()
    {
        return Interlocked.CompareExchange(ref _version, 0, 0);
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

    internal static void ResetForTesting()
    {
        _isEnabled = 0;
        _version = 0;
        _activeOperations = 0;
        _reporterStates.Clear();
        _filterStates.Clear();
        _configuration = new MonitoringConfiguration();
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

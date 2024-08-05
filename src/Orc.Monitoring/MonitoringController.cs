// ReSharper disable InconsistentNaming
#pragma warning disable IDE1006
namespace Orc.Monitoring;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Reflection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Provides centralized control for the monitoring system, including hierarchical control of reporters and filters.
/// </summary>
/// <remarks>
/// Usage examples:
/// 
/// 1. Enabling and disabling global monitoring:
/// <code>
/// MonitoringController.Enable();
/// // Monitoring is now globally enabled
/// MonitoringController.Disable();
/// // Monitoring is now globally disabled
/// </code>
/// 
/// 2. Enabling and disabling specific reporters or filters:
/// <code>
/// MonitoringController.EnableReporter(typeof(PerformanceReporter));
/// MonitoringController.DisableFilter(typeof(WorkflowItemFilter));
/// </code>
/// 
/// 3. Using temporary state changes:
/// <code>
/// using (MonitoringController.TemporarilyEnableReporter(typeof(PerformanceReporter)))
/// {
///     // PerformanceReporter is temporarily enabled here
/// }
/// // PerformanceReporter returns to its previous state
/// </code>
/// 
/// 4. Checking if monitoring should occur:
/// <code>
/// if (MonitoringController.ShouldTrack(operationVersion, typeof(PerformanceReporter), typeof(WorkflowItemFilter)))
/// {
///     // Perform monitoring
/// }
/// </code>
/// </remarks>
public static class MonitoringController
{
    private static int _isEnabled = 0;
    private static int _version = 0;
    private static int _activeOperations = 0;
    private static readonly ConcurrentDictionary<Type, bool> _reporterTrueStates = new();
    private static readonly ConcurrentDictionary<Type, bool> _filterTrueStates = new();
    private static readonly ConcurrentDictionary<Type, bool> _reporterEffectiveStates = new();
    private static readonly ConcurrentDictionary<Type, bool> _filterEffectiveStates = new();
    private static readonly ReaderWriterLockSlim _stateLock = new();

    private static readonly ILoggerFactory _loggerFactory = new LoggerFactory();
    private static readonly ILogger _logger = CreateLogger(typeof(MonitoringController));

    private static readonly ReaderWriterLockSlim _globalLock = new ReaderWriterLockSlim();
    private static readonly ConcurrentDictionary<Type, ReaderWriterLockSlim> _componentLocks = new ConcurrentDictionary<Type, ReaderWriterLockSlim>();
    private static readonly ConcurrentDictionary<Type, bool> _componentStateCache = new ConcurrentDictionary<Type, bool>();

    private static MonitoringConfiguration _configuration = new MonitoringConfiguration();

    public static ILogger<T> CreateLogger<T>() => _loggerFactory.CreateLogger<T>();
    public static ILogger CreateLogger(Type type) => _loggerFactory.CreateLogger(type);

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

    /// <summary>
    /// Enables global monitoring and restores individual component states.
    /// </summary>
    public static void Enable()
    {
        _stateLock.EnterWriteLock();
        try
        {
            if (Interlocked.Exchange(ref _isEnabled, 1) == 0)
            {
                RestoreComponentStates();
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
    /// Disables global monitoring and sets all component states to disabled.
    /// </summary>
    public static void Disable()
    {
        _stateLock.EnterWriteLock();
        try
        {
            if (Interlocked.Exchange(ref _isEnabled, 0) == 1)
            {
                DisableAllComponents();
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

    private static void RestoreComponentStates()
    {
        foreach (var kvp in _reporterTrueStates)
        {
            _reporterEffectiveStates[kvp.Key] = kvp.Value;
            InvalidateCache(kvp.Key);
        }
        foreach (var kvp in _filterTrueStates)
        {
            _filterEffectiveStates[kvp.Key] = kvp.Value;
            InvalidateCache(kvp.Key);
        }
    }

    private static void DisableAllComponents()
    {
        foreach (var key in _reporterEffectiveStates.Keys.ToList())
        {
            _reporterEffectiveStates[key] = false;
        }
        foreach (var key in _filterEffectiveStates.Keys.ToList())
        {
            _filterEffectiveStates[key] = false;
        }
    }

    private static void SetComponentState(MonitoringComponentType componentType, Type type, bool enabled)
    {
        _stateLock.EnterWriteLock();
        try
        {
            var trueStates = componentType == MonitoringComponentType.Reporter ? _reporterTrueStates : _filterTrueStates;
            var effectiveStates = componentType == MonitoringComponentType.Reporter ? _reporterEffectiveStates : _filterEffectiveStates;

            trueStates[type] = enabled;
            effectiveStates[type] = enabled && IsEnabled;
            InvalidateCache(type);
            int newVersion = Interlocked.Increment(ref _version);
            _logger.LogDebug($"{componentType} {type.Name} {(enabled ? "enabled" : "disabled")}. New version: {newVersion}");
            StateChanged?.Invoke(componentType, type.Name, effectiveStates[type], newVersion);
        }
        finally
        {
            _stateLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Enables a specific reporter.
    /// </summary>
    /// <param name="reporterType">The type of the reporter to enable.</param>
    public static void EnableReporter(Type reporterType) => SetComponentState(MonitoringComponentType.Reporter, reporterType, true);


    /// <summary>
    /// Disables a specific reporter.
    /// </summary>
    /// <param name="reporterType">The type of the reporter to disable.</param>
    public static void DisableReporter(Type reporterType) => SetComponentState(MonitoringComponentType.Reporter, reporterType, false);

    /// <summary>
    /// Checks if a specific reporter is enabled.
    /// </summary>
    /// <param name="reporterType">The type of the reporter to check.</param>
    /// <returns>True if the reporter is enabled, false otherwise.</returns>
    public static bool IsReporterEnabled(Type reporterType) => IsComponentEnabled(_reporterEffectiveStates, reporterType);

    /// <summary>
    /// Enables a specific filter.
    /// </summary>
    /// <param name="filterType">The type of the filter to enable.</param>
    public static void EnableFilter(Type filterType) => SetComponentState(MonitoringComponentType.Filter, filterType, true);


    /// <summary>
    /// Disables a specific filter.
    /// </summary>
    /// <param name="filterType">The type of the filter to disable.</param>
    public static void DisableFilter(Type filterType) => SetComponentState(MonitoringComponentType.Filter, filterType, false);


    /// <summary>
    /// Checks if a specific filter is enabled.
    /// </summary>
    /// <param name="filterType">The type of the filter to check.</param>
    /// <returns>True if the filter is enabled, false otherwise.</returns>
    public static bool IsFilterEnabled(Type filterType) => IsComponentEnabled(_filterEffectiveStates, filterType);

    private static ReaderWriterLockSlim GetComponentLock(Type componentType)
    {
        return _componentLocks.GetOrAdd(componentType, _ => new ReaderWriterLockSlim());
    }

    private static void EnableComponent(MonitoringComponentType componentType, ConcurrentDictionary<Type, bool> trueStates, ConcurrentDictionary<Type, bool> effectiveStates, Type type)
    {
        _stateLock.EnterWriteLock();
        try
        {
            trueStates[type] = true;
            effectiveStates[type] = IsEnabled;
            InvalidateCache(type);
            int newVersion = Interlocked.Increment(ref _version);
            _logger.LogDebug($"{componentType} {type.Name} enabled. New version: {newVersion}");
            StateChanged?.Invoke(componentType, type.Name, effectiveStates[type], newVersion);
        }
        finally
        {
            _stateLock.ExitWriteLock();
        }
    }

    private static void DisableComponent(MonitoringComponentType componentType, ConcurrentDictionary<Type, bool> trueStates, ConcurrentDictionary<Type, bool> effectiveStates, Type type)
    {
        _stateLock.EnterWriteLock();
        try
        {
            trueStates[type] = false;
            effectiveStates[type] = false;
            int newVersion = Interlocked.Increment(ref _version);
            _logger.LogDebug($"{componentType} {type.Name} disabled. New version: {newVersion}");
            StateChanged?.Invoke(componentType, type.Name, false, newVersion);
        }
        finally
        {
            _stateLock.ExitWriteLock();
        }
    }

    private static bool IsComponentEnabled(ConcurrentDictionary<Type, bool> statesDictionary, Type type)
    {
        return _componentStateCache.GetOrAdd(type, _ => statesDictionary.TryGetValue(type, out bool state) && state && IsEnabled);
    }

    private static void InvalidateCache(Type type)
    {
        _componentStateCache.TryRemove(type, out _);
    }

    /// <summary>
    /// Gets the current state of all monitored components.
    /// </summary>
    /// <returns>A dictionary containing the state of all reporters and filters.</returns>
    public static Dictionary<string, bool> GetAllComponentStates()
    {
        var states = new Dictionary<string, bool>();

        foreach (var kvp in _reporterEffectiveStates)
        {
            states[$"Reporter:{kvp.Key.Name}"] = kvp.Value;
        }

        foreach (var kvp in _filterEffectiveStates)
        {
            states[$"Filter:{kvp.Key.Name}"] = kvp.Value;
        }

        return states;
    }

    public static void RegisterCustomComponent(Type componentType, MonitoringComponentType type)
    {
        if (type == MonitoringComponentType.Reporter)
        {
            _reporterTrueStates[componentType] = false;
            _reporterEffectiveStates[componentType] = false;
        }
        else if (type == MonitoringComponentType.Filter)
        {
            _filterTrueStates[componentType] = false;
            _filterEffectiveStates[componentType] = false;
        }
    }

    private static IDisposable TemporarilyEnableComponent<T>(MonitoringComponentType componentType, ConcurrentDictionary<Type, bool> trueStates, ConcurrentDictionary<Type, bool> effectiveStates)
        where T : class
    {
        return new TemporaryStateChange(componentType, trueStates, effectiveStates, typeof(T));
    }

    public static IDisposable TemporarilyEnableReporter<T>() where T : class => TemporarilyEnableComponent<T>(MonitoringComponentType.Reporter, _reporterTrueStates, _reporterEffectiveStates);


    public static IDisposable TemporarilyEnableFilter<T>() where T : class => TemporarilyEnableComponent<T>(MonitoringComponentType.Filter, _filterTrueStates, _filterEffectiveStates);


    /// <summary>
    /// Begins a new monitoring operation.
    /// </summary>
    /// <returns>An IDisposable that will end the operation when disposed.</returns>
    public static IDisposable BeginOperation()
    {
        Interlocked.Increment(ref _activeOperations);
        return new OperationScope();
    }

    /// <summary>
    /// Determines whether monitoring should track the current operation based on the global state and component states.
    /// </summary>
    /// <param name="operationVersion">The version of the operation to check.</param>
    /// <param name="reporterType">The type of the reporter to check, if any.</param>
    /// <param name="filterType">The type of the filter to check, if any.</param>
    /// <returns>True if the operation should be tracked, false otherwise.</returns>
    public static bool ShouldTrack(int operationVersion, Type? reporterType = null, Type? filterType = null)
    {
        return IsEnabled
               && operationVersion == CurrentVersion
               && (reporterType is null || IsReporterEnabled(reporterType))
               && (filterType is null || IsFilterEnabled(filterType));
    }

    /// <summary>
    /// Checks if a specific method should be monitored.
    /// </summary>
    /// <param name="method">The method to check.</param>
    /// <returns>True if the method should be monitored, false otherwise.</returns>
    public static bool ShouldMonitor(MethodInfo method)
    {
        return IsEnabled && _configuration.ShouldMonitor(method);
    }

    /// <summary>
    /// Checks if a specific type should be monitored.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns>True if the type should be monitored, false otherwise.</returns>
    public static bool ShouldMonitor(Type type)
    {
        return IsEnabled && _configuration.ShouldMonitor(type);
    }

    /// <summary>
    /// Checks if a specific namespace should be monitored.
    /// </summary>
    /// <param name="namespace">The namespace to check.</param>
    /// <returns>True if the namespace should be monitored, false otherwise.</returns>
    public static bool ShouldMonitor(string @namespace)
    {
        return IsEnabled && _configuration.ShouldMonitor(@namespace);
    }

    /// <summary>
    /// Adds a callback to be invoked when the state of any monitoring component changes.
    /// </summary>
    /// <param name="callback">The callback to add.</param>
    public static void AddStateChangedCallback(Action<MonitoringComponentType, string, bool, int> callback)
    {
        StateChanged += callback;
    }

    /// <summary>
    /// Gets the current version of the monitoring state.
    /// </summary>
    /// <returns>The current version number.</returns>
    public static int GetCurrentVersion()
    {
        return Interlocked.CompareExchange(ref _version, 0, 0);
    }

    internal static void ResetForTesting()
    {
        _stateLock.EnterWriteLock();
        try
        {
            _isEnabled = 0;
            _version = 0;
            _activeOperations = 0;
            _reporterTrueStates.Clear();
            _reporterEffectiveStates.Clear();
            _filterTrueStates.Clear();
            _filterEffectiveStates.Clear();
            _configuration = new MonitoringConfiguration();
        }
        finally
        {
            _stateLock.ExitWriteLock();
        }
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
        private readonly ConcurrentDictionary<Type, bool> _trueStates;
        private readonly ConcurrentDictionary<Type, bool> _effectiveStates;
        private readonly Type _type;
        private readonly bool _originalTrueState;
        private readonly bool _originalEffectiveState;

        public TemporaryStateChange(MonitoringComponentType componentType, ConcurrentDictionary<Type, bool> trueStates, ConcurrentDictionary<Type, bool> effectiveStates, Type type)
        {
            _componentType = componentType;
            _trueStates = trueStates;
            _effectiveStates = effectiveStates;
            _type = type;
            _originalTrueState = trueStates.TryGetValue(type, out var trueState) ? trueState : false;
            _originalEffectiveState = effectiveStates.TryGetValue(type, out var effectiveState) ? effectiveState : false;

            EnableComponent(componentType, trueStates, effectiveStates, type);
        }

        public void Dispose()
        {
            _stateLock.EnterWriteLock();
            try
            {
                _trueStates[_type] = _originalTrueState;
                _effectiveStates[_type] = IsEnabled && _originalTrueState;
                InvalidateCache(_type);
                int newVersion = Interlocked.Increment(ref _version);
                StateChanged?.Invoke(_componentType, _type.Name, _effectiveStates[_type], newVersion);
            }
            finally
            {
                _stateLock.ExitWriteLock();
            }
        }
    }
}

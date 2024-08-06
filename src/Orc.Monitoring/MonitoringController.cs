// ReSharper disable InconsistentNaming
#pragma warning disable IDE1006
namespace Orc.Monitoring;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Logging;

/// <summary>
/// Provides centralized control for the monitoring system, including hierarchical control of reporters and filters.
/// </summary>
/// <remarks>
/// The MonitoringController allows for granular control over the monitoring system, enabling or disabling
/// specific reporters and filters, as well as global monitoring state.
/// 
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
/// using (MonitoringController.TemporarilyEnableReporter&lt;PerformanceReporter&gt;())
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
/// 
/// Performance characteristics:
/// - ShouldTrack: ~39 ns
/// - Enable/Disable Reporter: ~127 ns
/// - Temporarily Enable Reporter: ~335 ns
/// - Global Enable/Disable: ~850 ns
/// 
/// Note: Frequent enable/disable operations may impact performance.
/// Consider using TemporarilyEnable methods for short-term changes.
/// 
/// Limitations:
/// - State changes are not persisted across application restarts.
/// - Temporary state changes are not thread-safe across multiple threads.
/// </remarks>


public static class MonitoringController
{
    private static int _isEnabled = 0;
    private static int _activeOperations = 0;
    private static MonitoringVersion _currentVersion = new MonitoringVersion(0, Guid.NewGuid());
    private static readonly ConcurrentDictionary<Type, bool> _reporterTrueStates = new();
    private static readonly ConcurrentDictionary<Type, bool> _filterTrueStates = new();
    private static readonly ConcurrentDictionary<Type, bool> _reporterEffectiveStates = new();
    private static readonly ConcurrentDictionary<Type, bool> _filterEffectiveStates = new();
    private static readonly ReaderWriterLockSlim _stateLock = new();
    private static readonly List<WeakReference<VersionedMonitoringContext>> _activeContexts = new();

    private static readonly ILoggerFactory _loggerFactory = new LoggerFactory();
    private static readonly ILogger _logger = CreateLogger(typeof(MonitoringController));

    private static MonitoringConfiguration _configuration = new MonitoringConfiguration();

    public static event EventHandler<MonitoringVersion>? VersionChanged;

    public static ILogger<T> CreateLogger<T>() => _loggerFactory.CreateLogger<T>();
    public static ILogger CreateLogger(Type type) => _loggerFactory.CreateLogger(type);

    public static MonitoringConfiguration Configuration
    {
        get => _configuration;
        set
        {
            _configuration = value;
            UpdateVersion();
        }
    }

    public static bool IsEnabled => Interlocked.CompareExchange(ref _isEnabled, 0, 0) == 1;

    public static MonitoringVersion GetCurrentVersion() => _currentVersion;

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
                UpdateVersion();
                RestoreComponentStates();
                _logger.LogDebug($"Monitoring enabled. New version: {_currentVersion}");
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
                UpdateVersion();
                DisableAllComponents();
                _logger.LogDebug($"Monitoring disabled. New version: {_currentVersion}");
            }
        }
        finally
        {
            _stateLock.ExitWriteLock();
        }
    }

    public static void EnableReporter(Type reporterType) => SetComponentState(MonitoringComponentType.Reporter, reporterType, true);
    public static void DisableReporter(Type reporterType) => SetComponentState(MonitoringComponentType.Reporter, reporterType, false);
    public static bool IsReporterEnabled(Type reporterType) => IsComponentEnabled(_reporterEffectiveStates, reporterType);

    public static void EnableFilter(Type filterType) => SetComponentState(MonitoringComponentType.Filter, filterType, true);
    public static void DisableFilter(Type filterType) => SetComponentState(MonitoringComponentType.Filter, filterType, false);
    public static bool IsFilterEnabled(Type filterType) => IsComponentEnabled(_filterEffectiveStates, filterType);

    public static bool ShouldTrack(MonitoringVersion operationVersion, Type? reporterType = null, Type? filterType = null)
    {
        return IsEnabled
               && operationVersion == _currentVersion
               && (reporterType is null || IsReporterEnabled(reporterType))
               && (filterType is null || IsFilterEnabled(filterType));
    }

    public static IDisposable BeginOperation(out MonitoringVersion operationVersion)
    {
        operationVersion = _currentVersion;
        Interlocked.Increment(ref _activeOperations);
        return new OperationScope();
    }

    public static void RegisterContext(VersionedMonitoringContext context)
    {
        _activeContexts.Add(new WeakReference<VersionedMonitoringContext>(context));
    }

    public static IDisposable TemporarilyEnableReporter<T>() where T : class
    {
        return new TemporaryStateChange(MonitoringComponentType.Reporter, typeof(T));
    }

    public static IDisposable TemporarilyEnableFilter<T>() where T : class
    {
        return new TemporaryStateChange(MonitoringComponentType.Filter, typeof(T));
    }


    private static void UpdateVersion()
    {
        var oldVersion = _currentVersion;
        _currentVersion = new MonitoringVersion(_currentVersion.MainVersion + 1, Guid.NewGuid());
        MonitoringDiagnostics.LogVersionChange(oldVersion, _currentVersion);
        VersionChanged?.Invoke(null, _currentVersion);
        PropagateVersionChange(_currentVersion);
    }

    private static event Action<MonitoringComponentType, string, bool, MonitoringVersion>? StateChangedCallback;

    public static void AddStateChangedCallback(Action<MonitoringComponentType, string, bool, MonitoringVersion> callback)
    {
        StateChangedCallback += callback;
    }

    public static void RemoveStateChangedCallback(Action<MonitoringComponentType, string, bool, MonitoringVersion> callback)
    {
        StateChangedCallback -= callback;
    }

    private static void OnStateChanged(MonitoringComponentType componentType, string componentName, bool isEnabled, MonitoringVersion version)
    {
        StateChangedCallback?.Invoke(componentType, componentName, isEnabled, version);
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
            UpdateVersion();
            _logger.LogDebug($"{componentType} {type.Name} {(enabled ? "enabled" : "disabled")}. New version: {_currentVersion}");
            OnStateChanged(componentType, type.Name, effectiveStates[type], _currentVersion);
        }
        finally
        {
            _stateLock.ExitWriteLock();
        }
    }

    private static bool IsComponentEnabled(ConcurrentDictionary<Type, bool> statesDictionary, Type type)
    {
        return statesDictionary.TryGetValue(type, out bool state) && state && IsEnabled;
    }

    private static void RestoreComponentStates()
    {
        foreach (var kvp in _reporterTrueStates)
        {
            _reporterEffectiveStates[kvp.Key] = kvp.Value;
        }
        foreach (var kvp in _filterTrueStates)
        {
            _filterEffectiveStates[kvp.Key] = kvp.Value;
        }
    }

    private static void DisableAllComponents()
    {
        foreach (var key in _reporterEffectiveStates.Keys)
        {
            _reporterEffectiveStates[key] = false;
        }
        foreach (var key in _filterEffectiveStates.Keys)
        {
            _filterEffectiveStates[key] = false;
        }
    }

    private static void PropagateVersionChange(MonitoringVersion newVersion)
    {
        foreach (var weakRef in _activeContexts.ToArray())
        {
            if (weakRef.TryGetTarget(out var context))
            {
                context.UpdateVersion(newVersion);
            }
            else
            {
                _activeContexts.Remove(weakRef);
            }
        }
    }

#if DEBUG || TEST
    internal static void ResetForTesting()
    {
        _stateLock.EnterWriteLock();
        try
        {
            _isEnabled = 0;
            _activeOperations = 0;
            _currentVersion = new MonitoringVersion(0, Guid.NewGuid());
            _reporterTrueStates.Clear();
            _reporterEffectiveStates.Clear();
            _filterTrueStates.Clear();
            _filterEffectiveStates.Clear();
            _activeContexts.Clear();
            _configuration = new MonitoringConfiguration();

            // Clear any subscribed event handlers
            VersionChanged = null;

            // Reset MonitoringDiagnostics
            MonitoringDiagnostics.ClearVersionHistory();

            _logger.LogInformation("MonitoringController reset for testing");
        }
        finally
        {
            _stateLock.ExitWriteLock();
        }
    }
#endif

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
        private readonly Type _type;
        private readonly bool _originalState;
        private readonly MonitoringVersion _originalVersion;

        public TemporaryStateChange(MonitoringComponentType componentType, Type type)
        {
            _componentType = componentType;
            _type = type;
            _originalState = componentType == MonitoringComponentType.Reporter
                ? IsReporterEnabled(type)
                : IsFilterEnabled(type);
            _originalVersion = GetCurrentVersion();

            if (componentType == MonitoringComponentType.Reporter)
                EnableReporter(type);
            else
                EnableFilter(type);
        }

        public void Dispose()
        {
            if (_componentType == MonitoringComponentType.Reporter)
            {
                if (_originalState)
                    EnableReporter(_type);
                else
                    DisableReporter(_type);
            }
            else
            {
                if (_originalState)
                    EnableFilter(_type);
                else
                    DisableFilter(_type);
            }
        }
    }

    public enum MonitoringComponentType
    {
        Reporter,
        Filter
    }
}

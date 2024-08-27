// ReSharper disable InconsistentNaming
#pragma warning disable IDE1006
namespace Orc.Monitoring;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Logging;
using Filters;
using Reporters.ReportOutputs;
using Reporters;
using System.Linq;

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
    private static readonly VersionManager _versionManager = new();
    private static MonitoringVersion _currentVersion;
    private static int _isEnabled = 0;
    private static readonly ConcurrentDictionary<Type, bool> _reporterTrueStates = new();
    private static readonly ConcurrentDictionary<Type, bool> _filterTrueStates = new();
    private static readonly ConcurrentDictionary<Type, bool> _reporterEffectiveStates = new();
    private static readonly ConcurrentDictionary<Type, bool> _filterEffectiveStates = new();
    private static readonly ConcurrentDictionary<Type, bool> _outputTypeStates = new();
    private static readonly ReaderWriterLockSlim _stateLock = new(LockRecursionPolicy.SupportsRecursion);
    private static readonly List<WeakReference<VersionedMonitoringContext>> _activeContexts = new();
    private static readonly AsyncLocal<OperationContext?> _currentOperationContext = new();

    private static readonly ConcurrentDictionary<(MonitoringVersion, Type?, Type?, Type?, long), bool> _shouldTrackCache = new();
    private static readonly ConcurrentDictionary<(Type ReporterType, Type FilterType), bool> _reporterSpecificFilterStates = new();
    private static readonly ConcurrentDictionary<(string ReporterId, Type FilterType), bool> _reporterInstanceFilterStates = new();

    private static readonly ILoggerFactory _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
    private static readonly ILogger _logger = CreateLogger(typeof(MonitoringController));

    private static long _cacheVersion = 0;
    private static int _isUpdating = 0;

    private static MonitoringConfiguration _configuration = new();
    private static EnhancedDataPostProcessor? _enhancedDataPostProcessor;

    public static event EventHandler<VersionChangedEventArgs>? VersionChanged;

    public static ILogger<T> CreateLogger<T>() => _loggerFactory.CreateLogger<T>();
    public static ILogger CreateLogger(Type type) => _loggerFactory.CreateLogger(type);



    public static MonitoringConfiguration Configuration
    {
        get => _configuration;
        set
        {
            _stateLock.EnterWriteLock();
            try
            {
                var oldConfig = _configuration;
                _configuration = value;
                UpdateVersionNoLock();
                InvalidateShouldTrackCache();
                _logger.LogDebug($"Configuration updated. New version: {_currentVersion}");
                OnStateChanged(MonitoringComponentType.Configuration, "Configuration", true, _currentVersion);

                ApplyConfiguration(oldConfig, value);

                // Initialize or update EnhancedDataPostProcessor
                if (_enhancedDataPostProcessor is null)
                {
                    _enhancedDataPostProcessor = new EnhancedDataPostProcessor(CreateLogger<EnhancedDataPostProcessor>());
                }
                // Apply the new OrphanedNodeStrategy
                // Note: We don't need to do anything here as the strategy is read directly from the configuration when used
            }
            finally
            {
                _stateLock.ExitWriteLock();
            }
        }
    }

    public static EnhancedDataPostProcessor.OrphanedNodeStrategy GetOrphanedNodeStrategy()
    {
        return Configuration.OrphanedNodeStrategy;
    }

    public static EnhancedDataPostProcessor GetEnhancedDataPostProcessor()
    {
        if (_enhancedDataPostProcessor is null)
        {
            _stateLock.EnterWriteLock();
            try
            {
                if (_enhancedDataPostProcessor is null)
                {
                    _enhancedDataPostProcessor = new EnhancedDataPostProcessor(CreateLogger<EnhancedDataPostProcessor>());
                }
            }
            finally
            {
                _stateLock.ExitWriteLock();
            }
        }
        return _enhancedDataPostProcessor;
    }

    public static void EnableFilterForReporterType(Type reporterType, Type filterType)
    {
        SetFilterStateForReporterType(reporterType, filterType, true);
    }

    public static void DisableFilterForReporterType(Type reporterType, Type filterType)
    {
        SetFilterStateForReporterType(reporterType, filterType, false);
    }

    private static void SetFilterStateForReporterType(Type reporterType, Type filterType, bool enabled)
    {
        _reporterSpecificFilterStates[(reporterType, filterType)] = enabled;
        UpdateVersion();
    }

    public static bool IsFilterEnabledForReporterType(Type reporterType, Type filterType)
    {
        return _reporterSpecificFilterStates.TryGetValue((reporterType, filterType), out var isEnabled) && isEnabled;
    }

    public static void EnableFilterForReporter(string reporterId, Type filterType)
    {
        SetFilterStateForReporter(reporterId, filterType, true);
    }

    public static void DisableFilterForReporter(string reporterId, Type filterType)
    {
        SetFilterStateForReporter(reporterId, filterType, false);
    }

    private static void SetFilterStateForReporter(string reporterId, Type filterType, bool enabled)
    {
        _reporterInstanceFilterStates[(reporterId, filterType)] = enabled;
        UpdateVersion();
    }

    public static bool IsFilterEnabledForReporter(string reporterId, Type filterType)
    {
        return _reporterInstanceFilterStates.TryGetValue((reporterId, filterType), out var isEnabled) && isEnabled;
    }

    private static void ApplyConfiguration(MonitoringConfiguration oldConfig, MonitoringConfiguration newConfig)
    {
        // Apply global state
        SetGlobalState(newConfig.IsGloballyEnabled);

        // Apply filters
        foreach (var filter in newConfig.Filters)
        {
            EnableFilter(filter.GetType());
        }

        // Apply reporters
        foreach (var reporterType in newConfig.ReporterTypes)
        {
            EnableReporter(reporterType);
        }

        // Apply output type states
        foreach (var (outputType, isEnabled) in newConfig.OutputTypeStates)
        {
            if (isEnabled)
                EnableOutputType(outputType);
            else
                DisableOutputType(outputType);
        }

        _logger.LogDebug("New configuration applied");
    }

    public static bool IsEnabled => Interlocked.CompareExchange(ref _isEnabled, 0, 0) == 1;

    public static MonitoringVersion GetCurrentVersion()
    {
        _stateLock.EnterReadLock();
        try
        {
            return _currentVersion;
        }
        finally
        {
            _stateLock.ExitReadLock();
        }
    }

    public static void Enable()
    {
        SetGlobalState(true);
    }

    public static void Disable()
    {
        SetGlobalState(false);
    }

    private static void SetGlobalState(bool enabled)
    {
        if (Interlocked.Exchange(ref _isUpdating, 1) == 0)
        {
            try
            {
                _stateLock.EnterWriteLock();
                if (Interlocked.Exchange(ref _isEnabled, enabled ? 1 : 0) != (enabled ? 1 : 0))
                {
                    UpdateVersionNoLock();
                    if (enabled)
                        RestoreComponentStates();
                    else
                        DisableAllComponents();
                    _logger.LogDebug($"Monitoring {(enabled ? "enabled" : "disabled")}. New version: {_currentVersion}");
                }
            }
            finally
            {
                _stateLock.ExitWriteLock();
                Interlocked.Exchange(ref _isUpdating, 0);
            }
        }
    }

    public static void EnableReporter<T>() where T : IMethodCallReporter => EnableReporter(typeof(T));

    public static void DisableReporter<T>() where T : IMethodCallReporter => DisableReporter(typeof(T));

    public static bool IsReporterEnabled<T>() where T : IMethodCallReporter => IsReporterEnabled(typeof(T));

    public static void EnableReporter(Type reporterType)
    {
        if (!typeof(IMethodCallReporter).IsAssignableFrom(reporterType))
        {
            throw new ArgumentException("Type must implement IMethodCallReporter", nameof(reporterType));
        }

        SetComponentState(MonitoringComponentType.Reporter, reporterType, true);
    }

    public static void DisableReporter(Type reporterType)
    {
        if (!typeof(IMethodCallReporter).IsAssignableFrom(reporterType))
        {
            throw new ArgumentException("Type must implement IMethodCallReporter", nameof(reporterType));
        }

        SetComponentState(MonitoringComponentType.Reporter, reporterType, false);
    }

    public static bool IsReporterEnabled(Type reporterType)
    {
        if (!typeof(IMethodCallReporter).IsAssignableFrom(reporterType))
        {
            throw new ArgumentException("Type must implement IMethodCallReporter", nameof(reporterType));
        }

        return _reporterEffectiveStates.TryGetValue(reporterType, out var isEnabled) && isEnabled && IsEnabled;
    }

    public static void EnableFilter<T>() where T : IMethodFilter => EnableFilter(typeof(T));
    public static void DisableFilter<T>() where T : IMethodFilter => DisableFilter(typeof(T));
    public static bool IsFilterEnabled<T>() where T : IMethodFilter => IsFilterEnabled(typeof(T));

    public static void EnableFilter(Type filterType)
    {
        if (!typeof(IMethodFilter).IsAssignableFrom(filterType))
        {
            throw new ArgumentException($"Type {filterType.Name} does not implement IMethodFilter", nameof(filterType));
        }

        SetComponentState(MonitoringComponentType.Filter, filterType, true);
    }

    public static void DisableFilter(Type filterType)
    {
        if (!typeof(IMethodFilter).IsAssignableFrom(filterType))
        {
            throw new ArgumentException("Type must implement IFilter", nameof(filterType));
        }

        SetComponentState(MonitoringComponentType.Filter, filterType, false);
    }

    public static bool IsFilterEnabled(Type filterType)
    {
        if (!typeof(IMethodFilter).IsAssignableFrom(filterType))
        {
            throw new ArgumentException("Type must implement IFilter", nameof(filterType));
        }

        return IsComponentEnabled(_filterEffectiveStates, filterType);
    }

    public static void EnableOutputType<T>() where T : IReportOutput => EnableOutputType(typeof(T));
    public static void DisableOutputType<T>() where T : IReportOutput => DisableOutputType(typeof(T));
    public static bool IsOutputTypeEnabled<T>() where T : IReportOutput => IsOutputTypeEnabled(typeof(T));

    public static void EnableOutputType(Type outputType)
    {
        if (!typeof(IReportOutput).IsAssignableFrom(outputType))
        {
            throw new ArgumentException("Type must implement IReportOutput", nameof(outputType));
        }

        SetComponentState(MonitoringComponentType.OutputType, outputType, true);
    }

    public static void DisableOutputType(Type outputType)
    {
        if (!typeof(IReportOutput).IsAssignableFrom(outputType))
        {
            throw new ArgumentException("Type must implement IReportOutput", nameof(outputType));
        }

        SetComponentState(MonitoringComponentType.OutputType, outputType, false);
    }

    public static bool IsOutputTypeEnabled(Type outputType)
    {
        return _configuration.OutputTypeStates.TryGetValue(outputType, out var isEnabled) && isEnabled;
    }

    public static bool ShouldTrack(MonitoringVersion version, Type? reporterType = null, Type? filterType = null, IEnumerable<string>? reporterIds = null)
    {
        var shouldTrack = IsEnabled && version == GetCurrentVersion();
        _logger.LogDebug($"ShouldTrack called. IsEnabled: {IsEnabled}, VersionMatch: {version == GetCurrentVersion()}, Result: {shouldTrack}");

        if (!shouldTrack) return false;

        if (reporterType is not null)
        {
            shouldTrack = IsReporterEnabled(reporterType);
            _logger.LogDebug($"Reporter check. Type: {reporterType.Name}, Enabled: {shouldTrack}");
        }

        if (shouldTrack && filterType is not null)
        {
            if (reporterIds is not null)
            {
                shouldTrack = reporterIds.Any(id => IsFilterEnabledForReporter(id, filterType));
                _logger.LogDebug($"Filter check for reporters. FilterType: {filterType.Name}, Result: {shouldTrack}");
            }
            else if (reporterType is not null)
            {
                shouldTrack = IsFilterEnabledForReporterType(reporterType, filterType);
                _logger.LogDebug($"Filter check for reporter type. ReporterType: {reporterType.Name}, FilterType: {filterType.Name}, Result: {shouldTrack}");
            }
            else
            {
                shouldTrack = IsFilterEnabled(filterType);
                _logger.LogDebug($"General filter check. FilterType: {filterType.Name}, Result: {shouldTrack}");
            }
        }

        return shouldTrack;
    }

    private static void InvalidateShouldTrackCache()
    {
        Interlocked.Increment(ref _cacheVersion);
        _shouldTrackCache.Clear();
    }

    public static IDisposable BeginOperation(out MonitoringVersion operationVersion)
    {
        _stateLock.EnterReadLock();
        try
        {
            operationVersion = GetCurrentVersion();
            var parentContext = _currentOperationContext.Value;
            var newContext = new OperationContext(operationVersion, parentContext);
            _currentOperationContext.Value = newContext;
            _logger.LogDebug($"Operation begun with version: {operationVersion}");
            return new OperationScope(newContext);
        }
        finally
        {
            _stateLock.ExitReadLock();
        }
    }

    public static bool IsOperationValid()
    {
        var context = _currentOperationContext.Value;
        return context is not null && context.OperationVersion == GetCurrentVersion();
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

    public static string GenerateVersionReport()
    {
        return MonitoringDiagnostics.GenerateVersionReport();
    }

    private static void NotifyVersionChanged(MonitoringVersion oldVersion, MonitoringVersion newVersion)
    {
        VersionChanged?.Invoke(null, new VersionChangedEventArgs(oldVersion, newVersion));
    }

    private static void UpdateVersionNoLock()
    {
        var oldVersion = _currentVersion;
        _currentVersion = _versionManager.GetNextVersion();
        MonitoringDiagnostics.LogVersionChange(oldVersion, _currentVersion);
        NotifyVersionChanged(oldVersion, _currentVersion);
        PropagateVersionChange(_currentVersion);
        InvalidateShouldTrackCache();
    }

    private static void UpdateVersion()
    {
        if (Interlocked.Exchange(ref _isUpdating, 1) == 0)
        {
            try
            {
                _stateLock.EnterWriteLock();
                UpdateVersionNoLock();
            }
            finally
            {
                _stateLock.ExitWriteLock();
                Interlocked.Exchange(ref _isUpdating, 0);
            }
        }
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
        bool lockTaken = false;
        try
        {
            _stateLock.EnterWriteLock();
            lockTaken = true;

            switch (componentType)
            {
                case MonitoringComponentType.Reporter:
                    _reporterTrueStates[type] = enabled;
                    _reporterEffectiveStates[type] = enabled && IsEnabled;
                    break;
                case MonitoringComponentType.Filter:
                    _filterTrueStates[type] = enabled;
                    _filterEffectiveStates[type] = enabled && IsEnabled;
                    break;
                case MonitoringComponentType.OutputType:
                    _configuration.SetOutputTypeState(type, enabled);
                    break;
            }

            UpdateVersionNoLock();
            _logger.LogDebug($"{componentType} {type.Name} {(enabled ? "enabled" : "disabled")}. New version: {_currentVersion}");
            OnStateChanged(componentType, type.Name, enabled, _currentVersion);
            InvalidateShouldTrackCache();
        }
        finally
        {
            if (lockTaken)
            {
                _stateLock.ExitWriteLock();
            }
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
        foreach (var key in _outputTypeStates.Keys)
        {
            _outputTypeStates[key] = false;
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

    private class OperationContext
    {
        public MonitoringVersion OperationVersion { get; }
        public OperationContext? ParentContext { get; }

        public OperationContext(MonitoringVersion operationVersion, OperationContext? parentContext)
        {
            OperationVersion = operationVersion;
            ParentContext = parentContext;
        }
    }

    private sealed class OperationScope : IDisposable
    {
        private readonly OperationContext _context;
        private bool _isDisposed;

        public OperationScope(OperationContext context)
        {
            _context = context;
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _currentOperationContext.Value = _context.ParentContext;
                _isDisposed = true;
                _logger.LogDebug($"Operation ended with version: {_context.OperationVersion}");
            }
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
        Filter,
        OutputType,
        Configuration
    }

    #region Testing
    internal static void ResetForTesting()
    {
        _stateLock.EnterWriteLock();
        try
        {
            _isEnabled = 0;
            _currentVersion = default;
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
    #endregion
}

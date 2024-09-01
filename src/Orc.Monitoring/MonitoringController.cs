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

public class MonitoringController : IMonitoringController
{
    private readonly Func<EnhancedDataPostProcessor> _enhancedDataPostProcessorFactory;
    private readonly VersionManager _versionManager = new();
    private MonitoringVersion _currentVersion;
    private int _isEnabled = 0;
    private readonly ConcurrentDictionary<Type, bool> _reporterTrueStates = new();
    private readonly ConcurrentDictionary<Type, bool> _filterTrueStates = new();
    private readonly ConcurrentDictionary<Type, bool> _reporterEffectiveStates = new();
    private readonly ConcurrentDictionary<Type, bool> _filterEffectiveStates = new();
    private readonly ConcurrentDictionary<Type, bool> _outputTypeStates = new();
    private readonly ReaderWriterLockSlim _stateLock = new(LockRecursionPolicy.SupportsRecursion);
    private readonly List<WeakReference<VersionedMonitoringContext>> _activeContexts = new();
    private readonly AsyncLocal<OperationContext?> _currentOperationContext = new();

    private readonly ConcurrentDictionary<(MonitoringVersion, Type?, Type?, Type?, long), bool> _shouldTrackCache = new();
    private readonly ConcurrentDictionary<(Type ReporterType, Type FilterType), bool> _reporterSpecificFilterStates = new();
    private readonly ConcurrentDictionary<(string ReporterId, Type FilterType), bool> _reporterInstanceFilterStates = new();

    private readonly ILogger _logger;

    private long _cacheVersion = 0;
    private int _isUpdating = 0;

    private MonitoringConfiguration _configuration = new();
    private EnhancedDataPostProcessor? _enhancedDataPostProcessor;

    public event EventHandler<VersionChangedEventArgs>? VersionChanged;

    public MonitoringController(IMonitoringLoggerFactory loggerFactory, Func<EnhancedDataPostProcessor> enhancedDataPostProcessorFactory)
    {
        _enhancedDataPostProcessorFactory = enhancedDataPostProcessorFactory;
        _logger = loggerFactory.CreateLogger(typeof(MonitoringController));
    }

    public MonitoringConfiguration Configuration
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
            }
            finally
            {
                _stateLock.ExitWriteLock();
            }
        }
    }

    public EnhancedDataPostProcessor GetEnhancedDataPostProcessor()
    {
        if (_enhancedDataPostProcessor is not null)
        {
            return _enhancedDataPostProcessor;
        }

        _stateLock.EnterWriteLock();

        try
        {
            _enhancedDataPostProcessor ??= _enhancedDataPostProcessorFactory();
        }
        finally
        {
            _stateLock.ExitWriteLock();
        }

        return _enhancedDataPostProcessor;
    }

    public void EnableFilterForReporterType(Type reporterType, Type filterType)
    {
        SetFilterStateForReporterType(reporterType, filterType, true);
    }

    public void DisableFilterForReporterType(Type reporterType, Type filterType)
    {
        SetFilterStateForReporterType(reporterType, filterType, false);
    }

    private void SetFilterStateForReporterType(Type reporterType, Type filterType, bool enabled)
    {
        _reporterSpecificFilterStates[(reporterType, filterType)] = enabled;
        UpdateVersion();
    }

    public bool IsFilterEnabledForReporterType(Type reporterType, Type filterType)
    {
        return _reporterSpecificFilterStates.TryGetValue((reporterType, filterType), out var isEnabled) && isEnabled;
    }

    public void EnableFilterForReporter(string reporterId, Type filterType)
    {
        SetFilterStateForReporter(reporterId, filterType, true);
    }

    public void DisableFilterForReporter(string reporterId, Type filterType)
    {
        SetFilterStateForReporter(reporterId, filterType, false);
    }

    private void SetFilterStateForReporter(string reporterId, Type filterType, bool enabled)
    {
        _reporterInstanceFilterStates[(reporterId, filterType)] = enabled;
        UpdateVersion();
    }

    public bool IsFilterEnabledForReporter(string reporterId, Type filterType)
    {
        return _reporterInstanceFilterStates.TryGetValue((reporterId, filterType), out var isEnabled) && isEnabled;
    }

    private void ApplyConfiguration(MonitoringConfiguration oldConfig, MonitoringConfiguration newConfig)
    {
        SetGlobalState(newConfig.IsGloballyEnabled);

        foreach (var filter in newConfig.Filters)
        {
            EnableFilter(filter.GetType());
        }

        foreach (var reporterType in newConfig.ReporterTypes)
        {
            EnableReporter(reporterType);
        }

        foreach (var (outputType, isEnabled) in newConfig.OutputTypeStates)
        {
            if (isEnabled)
                EnableOutputType(outputType);
            else
                DisableOutputType(outputType);
        }

        _logger.LogDebug("New configuration applied");
    }

    public bool IsEnabled => Interlocked.CompareExchange(ref _isEnabled, 0, 0) == 1;

    public MonitoringVersion GetCurrentVersion()
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

    public void Enable()
    {
        SetGlobalState(true);
    }

    public void Disable()
    {
        SetGlobalState(false);
    }

    private void SetGlobalState(bool enabled)
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

    public void EnableReporter<T>() where T : IMethodCallReporter => EnableReporter(typeof(T));

    public void DisableReporter<T>() where T : IMethodCallReporter => DisableReporter(typeof(T));

    public bool IsReporterEnabled<T>() where T : IMethodCallReporter => IsReporterEnabled(typeof(T));

    public void EnableReporter(Type reporterType)
    {
        if (!typeof(IMethodCallReporter).IsAssignableFrom(reporterType))
        {
            throw new ArgumentException("Type must implement IMethodCallReporter", nameof(reporterType));
        }

        SetComponentState(MonitoringComponentType.Reporter, reporterType, true);
    }

    public void DisableReporter(Type reporterType)
    {
        if (!typeof(IMethodCallReporter).IsAssignableFrom(reporterType))
        {
            throw new ArgumentException("Type must implement IMethodCallReporter", nameof(reporterType));
        }

        SetComponentState(MonitoringComponentType.Reporter, reporterType, false);
    }

    public bool IsReporterEnabled(Type reporterType)
    {
        if (!typeof(IMethodCallReporter).IsAssignableFrom(reporterType))
        {
            throw new ArgumentException("Type must implement IMethodCallReporter", nameof(reporterType));
        }

        return _reporterEffectiveStates.TryGetValue(reporterType, out var isEnabled) && isEnabled && IsEnabled;
    }

    public void EnableFilter<T>() where T : IMethodFilter => EnableFilter(typeof(T));
    public void DisableFilter<T>() where T : IMethodFilter => DisableFilter(typeof(T));
    public bool IsFilterEnabled<T>() where T : IMethodFilter => IsFilterEnabled(typeof(T));

    public void EnableFilter(Type filterType)
    {
        if (!typeof(IMethodFilter).IsAssignableFrom(filterType))
        {
            throw new ArgumentException($"Type {filterType.Name} does not implement IMethodFilter", nameof(filterType));
        }

        SetComponentState(MonitoringComponentType.Filter, filterType, true);
    }

    public void DisableFilter(Type filterType)
    {
        if (!typeof(IMethodFilter).IsAssignableFrom(filterType))
        {
            throw new ArgumentException("Type must implement IFilter", nameof(filterType));
        }

        SetComponentState(MonitoringComponentType.Filter, filterType, false);
    }

    public bool IsFilterEnabled(Type filterType)
    {
        if (!typeof(IMethodFilter).IsAssignableFrom(filterType))
        {
            throw new ArgumentException("Type must implement IFilter", nameof(filterType));
        }

        return IsComponentEnabled(_filterEffectiveStates, filterType);
    }

    public void EnableOutputType<T>() where T : IReportOutput => EnableOutputType(typeof(T));
    public void DisableOutputType<T>() where T : IReportOutput => DisableOutputType(typeof(T));
    public bool IsOutputTypeEnabled<T>() where T : IReportOutput => IsOutputTypeEnabled(typeof(T));

    public void EnableOutputType(Type outputType)
    {
        if (!typeof(IReportOutput).IsAssignableFrom(outputType))
        {
            throw new ArgumentException("Type must implement IReportOutput", nameof(outputType));
        }

        SetComponentState(MonitoringComponentType.OutputType, outputType, true);
    }

    public void DisableOutputType(Type outputType)
    {
        if (!typeof(IReportOutput).IsAssignableFrom(outputType))
        {
            throw new ArgumentException("Type must implement IReportOutput", nameof(outputType));
        }

        SetComponentState(MonitoringComponentType.OutputType, outputType, false);
    }

    public bool IsOutputTypeEnabled(Type outputType)
    {
        return _configuration.OutputTypeStates.TryGetValue(outputType, out var isEnabled) && isEnabled;
    }

    public bool ShouldTrack(MonitoringVersion version, Type? reporterType = null, Type? filterType = null, IEnumerable<string>? reporterIds = null)
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

    private void InvalidateShouldTrackCache()
    {
        Interlocked.Increment(ref _cacheVersion);
        _shouldTrackCache.Clear();
    }

    public IDisposable BeginOperation(out MonitoringVersion operationVersion)
    {
        _stateLock.EnterReadLock();
        try
        {
            operationVersion = GetCurrentVersion();
            var parentContext = _currentOperationContext.Value;
            var newContext = new OperationContext(operationVersion, parentContext);
            _currentOperationContext.Value = newContext;
            _logger.LogDebug($"Operation begun with version: {operationVersion}");
            return new OperationScope(this, newContext);
        }
        finally
        {
            _stateLock.ExitReadLock();
        }
    }

    public bool IsOperationValid()
    {
        var context = _currentOperationContext.Value;
        return context is not null && context.OperationVersion == GetCurrentVersion();
    }

    public void RegisterContext(VersionedMonitoringContext context)
    {
        _activeContexts.Add(new WeakReference<VersionedMonitoringContext>(context));
    }

    public IDisposable TemporarilyEnableReporter<T>() where T : class
    {
        return new TemporaryStateChange(MonitoringComponentType.Reporter, typeof(T), this);
    }

    public IDisposable TemporarilyEnableFilter<T>() where T : class
    {
        return new TemporaryStateChange(MonitoringComponentType.Filter, typeof(T), this);
    }

    public string GenerateVersionReport()
    {
        return MonitoringDiagnostics.GenerateVersionReport();
    }

    private void NotifyVersionChanged(MonitoringVersion oldVersion, MonitoringVersion newVersion)
    {
        VersionChanged?.Invoke(this, new VersionChangedEventArgs(oldVersion, newVersion));
    }

    private void UpdateVersionNoLock()
    {
        var oldVersion = _currentVersion;
        _currentVersion = _versionManager.GetNextVersion();
        MonitoringDiagnostics.LogVersionChange(oldVersion, _currentVersion);
        NotifyVersionChanged(oldVersion, _currentVersion);
        PropagateVersionChange(_currentVersion);
        InvalidateShouldTrackCache();
    }

    private void UpdateVersion()
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

    private event Action<MonitoringComponentType, string, bool, MonitoringVersion>? StateChangedCallback;

    public void AddStateChangedCallback(Action<MonitoringComponentType, string, bool, MonitoringVersion> callback)
    {
        StateChangedCallback += callback;
    }

    public void RemoveStateChangedCallback(Action<MonitoringComponentType, string, bool, MonitoringVersion> callback)
    {
        StateChangedCallback -= callback;
    }

    private void OnStateChanged(MonitoringComponentType componentType, string componentName, bool isEnabled, MonitoringVersion version)
    {
        StateChangedCallback?.Invoke(componentType, componentName, isEnabled, version);
    }

    private void SetComponentState(MonitoringComponentType componentType, Type type, bool enabled)
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

    private bool IsComponentEnabled(ConcurrentDictionary<Type, bool> statesDictionary, Type type)
    {
        return statesDictionary.TryGetValue(type, out bool state) && state && IsEnabled;
    }

    private void RestoreComponentStates()
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

    private void DisableAllComponents()
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

    private void PropagateVersionChange(MonitoringVersion newVersion)
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
        private readonly MonitoringController _monitoringController;
        private readonly OperationContext _context;
        private bool _isDisposed;

        public OperationScope(MonitoringController monitoringController, OperationContext context)
        {
            _monitoringController = monitoringController;
            _context = context;
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _monitoringController._currentOperationContext.Value = _context.ParentContext;
                _isDisposed = true;
                _monitoringController._logger.LogDebug($"Operation ended with version: {_context.OperationVersion}");
            }
        }
    }

    private sealed class TemporaryStateChange : IDisposable
    {
        private readonly MonitoringComponentType _componentType;
        private readonly Type _type;
        private readonly bool _originalState;
        private readonly MonitoringVersion _originalVersion;
        private readonly MonitoringController _controller;

        public TemporaryStateChange(MonitoringComponentType componentType, Type type, MonitoringController controller)
        {
            _componentType = componentType;
            _type = type;
            _controller = controller;
            _originalState = componentType == MonitoringComponentType.Reporter
                ? _controller.IsReporterEnabled(type)
                : _controller.IsFilterEnabled(type);
            _originalVersion = _controller.GetCurrentVersion();

            if (componentType == MonitoringComponentType.Reporter)
                _controller.EnableReporter(type);
            else
                _controller.EnableFilter(type);
        }

        public void Dispose()
        {
            if (_componentType == MonitoringComponentType.Reporter)
            {
                if (_originalState)
                    _controller.EnableReporter(_type);
                else
                    _controller.DisableReporter(_type);
            }
            else
            {
                if (_originalState)
                    _controller.EnableFilter(_type);
                else
                    _controller.DisableFilter(_type);
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
}

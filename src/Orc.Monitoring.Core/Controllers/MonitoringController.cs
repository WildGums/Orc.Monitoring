// ReSharper disable InconsistentNaming
#pragma warning disable IDE1006
namespace Orc.Monitoring.Core.Controllers;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Abstractions;
using Configuration;
using Diagnostics;
using Microsoft.Extensions.Logging;
using Models;
using Utilities.Logging;
using Versioning;

public class MonitoringController(IMonitoringLoggerFactory loggerFactory) : IMonitoringController
{
    private readonly VersionManager _versionManager = new();
    private MonitoringVersion _currentVersion;
    private int _isEnabled;
    private readonly ConcurrentDictionary<Type, bool> _reporterTrueStates = new();
    private readonly ConcurrentDictionary<Type, bool> _filterTrueStates = new();
    private readonly ConcurrentDictionary<Type, bool> _reporterEffectiveStates = new();
    private readonly ConcurrentDictionary<Type, bool> _filterEffectiveStates = new();
    private readonly ConcurrentDictionary<Type, bool> _outputTypeStates = new();
    private readonly ReaderWriterLockSlim _stateLock = new(LockRecursionPolicy.SupportsRecursion);
    private readonly List<WeakReference<VersionedMonitoringContext>> _activeContexts = [];
    private readonly AsyncLocal<OperationContext?> _currentOperationContext = new();

    private readonly ConcurrentDictionary<(Type ReporterType, Type FilterType), bool> _reporterSpecificFilterStates = new();
    private readonly ConcurrentDictionary<(string ReporterId, Type FilterType), bool> _reporterInstanceFilterStates = new();

    private readonly ILogger _logger = loggerFactory.CreateLogger(typeof(MonitoringController));

    private long _cacheVersion;
    private int _isUpdating;

    private MonitoringConfiguration _configuration = new();

    public event EventHandler<VersionChangedEventArgs>? VersionChanged;

    public static IMonitoringController Instance { get; } = new MonitoringController(MonitoringLoggerFactory.Instance);

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

    public void SetFilterStateForReporterType(Type reporterType, Type filterType, bool enabled)
    {
        _reporterSpecificFilterStates[(reporterType, filterType)] = enabled;
        UpdateVersion();
    }

    public bool IsFilterEnabledForReporterType(Type reporterType, Type filterType)
    {
        return _reporterSpecificFilterStates.TryGetValue((reporterType, filterType), out var isEnabled) && isEnabled;
    }

    public void SetFilterStateForReporter(string reporterId, Type filterType, bool enabled)
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

    private void InvalidateShouldTrackCache()
    {
        Interlocked.Increment(ref _cacheVersion);
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

    private void OnStateChanged(MonitoringComponentType componentType, string componentName, bool isEnabled, MonitoringVersion version)
    {
        StateChangedCallback?.Invoke(componentType, componentName, isEnabled, version);
    }

    public void SetComponentState(MonitoringComponentType componentType, Type type, bool enabled)
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

    public bool GetComponentState(MonitoringComponentType componentType, Type type)
    {
        return componentType switch
        {
            MonitoringComponentType.Reporter => _reporterEffectiveStates.TryGetValue(type, out var isEnabled) && isEnabled && IsEnabled,
            MonitoringComponentType.Filter => _filterEffectiveStates.TryGetValue(type, out var isEnabled) && isEnabled && IsEnabled,
            MonitoringComponentType.OutputType => _configuration.OutputTypeStates.TryGetValue(type, out var isEnabled) && isEnabled,
            _ => throw new ArgumentOutOfRangeException(nameof(componentType), componentType, null)
        };
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

    private class OperationContext(MonitoringVersion operationVersion, OperationContext? parentContext)
    {
        public MonitoringVersion OperationVersion { get; } = operationVersion;
        public OperationContext? ParentContext { get; } = parentContext;
    }

    private sealed class OperationScope(MonitoringController monitoringController, OperationContext context) : IDisposable
    {
        private bool _isDisposed;

        public void Dispose()
        {
            if (!_isDisposed)
            {
                monitoringController._currentOperationContext.Value = context.ParentContext;
                _isDisposed = true;
                monitoringController._logger.LogDebug($"Operation ended with version: {context.OperationVersion}");
            }
        }
    }

    private sealed class TemporaryStateChange : IDisposable
    {
        private readonly MonitoringComponentType _componentType;
        private readonly Type _type;
        private readonly bool _originalState;
        private readonly IMonitoringController _controller;

        public TemporaryStateChange(MonitoringComponentType componentType, Type type, IMonitoringController controller)
        {
            _componentType = componentType;
            _type = type;
            _controller = controller;
            _originalState = _controller.GetComponentState(componentType, type);

            // Enable the component temporarily
            _controller.SetComponentState(componentType, type, true);
        }

        public void Dispose()
        {
            // Restore the original state
            _controller.SetComponentState(_componentType, _type, _originalState);
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

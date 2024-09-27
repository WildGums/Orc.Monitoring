namespace Orc.Monitoring.Core.Controllers;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Orc.Monitoring.Core.Abstractions;
using Orc.Monitoring.Core.Configuration;
using Orc.Monitoring.Core.Models;
using Orc.Monitoring.Core.Versioning;
using Utilities.Logging;

/// <summary>
/// The monitoring controller that manages the state and versioning of monitoring components.
/// </summary>
public class MonitoringController : IMonitoringController
{
    private readonly ILogger _logger;
    private readonly VersionManager _versionManager = new();
    private MonitoringVersion _currentVersion;
    private int _isEnabled;
    private readonly ReaderWriterLockSlim _stateLock = new(LockRecursionPolicy.SupportsRecursion);

    // Holds the enabled/disabled state of components.
    private readonly ConcurrentDictionary<Type, bool> _componentStates = new();

    // Manages active monitoring contexts.
    private readonly List<WeakReference<VersionedMonitoringContext>> _activeContexts = new();


    // Stores callbacks for state changes.
    private event Action<ComponentStateChangedEventArgs>? StateChangedCallback;

    private MonitoringConfiguration _configuration = new();

    // Stores the root operation scope.
    private OperationScope? _rootOperationScope;

    /// <summary>
    /// Initializes a new instance of the <see cref="MonitoringController"/> class.
    /// </summary>
    /// <param name="loggerFactory">The logger factory to create loggers.</param>
    public MonitoringController(IMonitoringLoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<MonitoringController>();
        _currentVersion = _versionManager.GetNextVersion();
    }

    /// <summary>
    /// Gets the singleton instance of the <see cref="MonitoringController"/>.
    /// </summary>
    public static IMonitoringController Instance { get; } = new MonitoringController(MonitoringLoggerFactory.Instance);

    /// <inheritdoc/>
    public bool IsEnabled => Interlocked.CompareExchange(ref _isEnabled, 0, 0) == 1;

    /// <inheritdoc/>
    public event EventHandler<VersionChangedEventArgs>? VersionChanged;

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public void Enable()
    {
        SetGlobalState(true);
    }

    /// <inheritdoc/>
    public void Disable()
    {
        SetGlobalState(false);
    }


    /// <inheritdoc/>
    public IDisposable BeginOperation(out MonitoringVersion operationVersion)
    {
        _stateLock.EnterReadLock();
        try
        {
            operationVersion = GetCurrentVersion();
            return new OperationScope(this);
        }
        finally
        {
            _stateLock.ExitReadLock();
        }
    }

    /// <inheritdoc/>
    public void RegisterContext(VersionedMonitoringContext context)
    {
        _activeContexts.Add(new WeakReference<VersionedMonitoringContext>(context));
    }

    /// <inheritdoc/>
    public MonitoringConfiguration Configuration
    {
        get => _configuration;
        set
        {
            _stateLock.EnterWriteLock();
            try
            {
                _configuration = value ?? throw new ArgumentNullException(nameof(value));
                UpdateVersionNoLock();
                OnStateChanged(new ComponentStateChangedEventArgs(null, true, _currentVersion));
            }
            finally
            {
                _stateLock.ExitWriteLock();
            }
        }
    }

    /// <inheritdoc/>
    public void AddStateChangedCallback(Action<ComponentStateChangedEventArgs> callback)
    {
        if (callback is null) throw new ArgumentNullException(nameof(callback));
        StateChangedCallback += callback;
    }

    /// <inheritdoc/>
    public void SetComponentState(Type componentType, bool enabled)
    {
        if (componentType is null) throw new ArgumentNullException(nameof(componentType));

        _stateLock.EnterWriteLock();
        try
        {
            _componentStates[componentType] = enabled;
            UpdateVersionNoLock();
            OnStateChanged(new ComponentStateChangedEventArgs(componentType, enabled, _currentVersion));
            _logger.LogDebug($"Component {componentType.Name} {(enabled ? "enabled" : "disabled")}. New version: {_currentVersion}");
        }
        finally
        {
            _stateLock.ExitWriteLock();
        }
    }

    /// <inheritdoc/>
    public bool GetComponentState(Type componentType)
    {
        if (componentType is null) throw new ArgumentNullException(nameof(componentType));

        _stateLock.EnterReadLock();
        try
        {
            return IsEnabled &&
                   _componentStates.TryGetValue(componentType, out var isEnabled) &&
                   isEnabled;
        }
        finally
        {
            _stateLock.ExitReadLock();
        }
    }

    public bool IsOperationValid(MonitoringVersion operationVersion)
    {
        return IsEnabled && operationVersion == _currentVersion && _rootOperationScope is not null;
    }

    private void SetGlobalState(bool enabled)
    {
        _stateLock.EnterWriteLock();
        try
        {
            Interlocked.Exchange(ref _isEnabled, enabled ? 1 : 0);
            UpdateVersionNoLock();
            OnStateChanged(new ComponentStateChangedEventArgs(null, enabled, _currentVersion));
            _logger.LogDebug($"Monitoring globally {(enabled ? "enabled" : "disabled")}. New version: {_currentVersion}");
        }
        finally
        {
            _stateLock.ExitWriteLock();
        }
    }

    private void UpdateVersionNoLock()
    {
        var oldVersion = _currentVersion;
        _currentVersion = _versionManager.GetNextVersion();
        VersionChanged?.Invoke(this, new VersionChangedEventArgs(oldVersion, _currentVersion));
        _logger.LogDebug($"Monitoring version updated from {oldVersion} to {_currentVersion}");
        PropagateVersionChange(_currentVersion);
    }

    private void PropagateVersionChange(MonitoringVersion newVersion)
    {
        foreach (var weakRef in _activeContexts.ToList())
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

    private void OnStateChanged(ComponentStateChangedEventArgs args)
    {
        StateChangedCallback?.Invoke(args);
    }

    /// <summary>
    /// Represents a scope for an operation, managing its lifecycle.
    /// </summary>
    private sealed class OperationScope : IDisposable
    {
        private readonly MonitoringController _controller;
        private bool _disposed;

        public OperationScope(MonitoringController controller)
        {
            _controller = controller;
            _controller._logger.LogDebug("Operation scope started.");

            if (Equals(_controller._rootOperationScope, null))
            {
                _controller._rootOperationScope = this;
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _controller._logger.LogDebug("Operation scope ended.");

                if (Equals(_controller._rootOperationScope, this))
                {
                    _controller.UpdateVersionNoLock();
                    _controller._rootOperationScope = null;
                }

                _disposed = true;
            }
        }
    }
}

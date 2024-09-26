namespace Orc.Monitoring.Core.Controllers;

using System;
using System.Threading.Tasks;
using Orc.Monitoring.Core.Abstractions;
using Orc.Monitoring.Core.Models;

/// <summary>
/// Extension methods for the <see cref="IMonitoringController"/> interface.
/// </summary>
public static class IMonitoringControllerExtensions
{
    /// <summary>
    /// Enables a component of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type of the component to enable.</typeparam>
    /// <param name="controller">The monitoring controller.</param>
    public static void EnableComponent<T>(this IMonitoringController controller) where T : IMonitoringComponent
    {
        controller.SetComponentState(typeof(T), true);
    }

    /// <summary>
    /// Disables a component of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type of the component to disable.</typeparam>
    /// <param name="controller">The monitoring controller.</param>
    public static void DisableComponent<T>(this IMonitoringController controller) where T : IMonitoringComponent
    {
        controller.SetComponentState(typeof(T), false);
    }

    /// <summary>
    /// Checks if a component of type <typeparamref name="T"/> is enabled.
    /// </summary>
    /// <typeparam name="T">The type of the component to check.</typeparam>
    /// <param name="controller">The monitoring controller.</param>
    /// <returns>True if the component is enabled; otherwise, false.</returns>
    public static bool IsComponentEnabled<T>(this IMonitoringController controller) where T : IMonitoringComponent
    {
        return controller.GetComponentState(typeof(T));
    }

    /// <summary>
    /// Enables a component of the specified type.
    /// </summary>
    /// <param name="controller">The monitoring controller.</param>
    /// <param name="componentType">The type of the component to enable.</param>
    public static void EnableComponent(this IMonitoringController controller, Type componentType)
    {
        ValidateComponentType(componentType);
        controller.SetComponentState(componentType, true);
    }

    /// <summary>
    /// Disables a component of the specified type.
    /// </summary>
    /// <param name="controller">The monitoring controller.</param>
    /// <param name="componentType">The type of the component to disable.</param>
    public static void DisableComponent(this IMonitoringController controller, Type componentType)
    {
        ValidateComponentType(componentType);
        controller.SetComponentState(componentType, false);
    }

    /// <summary>
    /// Checks if a component of the specified type is enabled.
    /// </summary>
    /// <param name="controller">The monitoring controller.</param>
    /// <param name="componentType">The type of the component to check.</param>
    /// <returns>True if the component is enabled; otherwise, false.</returns>
    public static bool IsComponentEnabled(this IMonitoringController controller, Type componentType)
    {
        ValidateComponentType(componentType);
        return controller.GetComponentState(componentType);
    }

    /// <summary>
    /// Applies a filter of type <typeparamref name="TFilter"/> to a reporter of type <typeparamref name="TReporter"/>.
    /// </summary>
    /// <typeparam name="TFilter">The type of the filter.</typeparam>
    /// <typeparam name="TReporter">The type of the reporter.</typeparam>
    /// <param name="controller">The monitoring controller.</param>
    public static void ApplyFilterToReporter<TFilter, TReporter>(this IMonitoringController controller)
        where TFilter : IMethodFilter
        where TReporter : IMethodCallReporter
    {
        var config = controller.Configuration;
        config.ComponentRegistry.AddRelationship(typeof(TReporter), typeof(TFilter));
    }

    /// <summary>
    /// Removes a filter of type <typeparamref name="TFilter"/> from a reporter of type <typeparamref name="TReporter"/>.
    /// </summary>
    /// <typeparam name="TFilter">The type of the filter.</typeparam>
    /// <typeparam name="TReporter">The type of the reporter.</typeparam>
    /// <param name="controller">The monitoring controller.</param>
    public static void RemoveFilterFromReporter<TFilter, TReporter>(this IMonitoringController controller)
        where TFilter : IMethodFilter
        where TReporter : IMethodCallReporter
    {
        var config = controller.Configuration;
        config.ComponentRegistry.RemoveRelationship(typeof(TReporter), typeof(TFilter));
    }

    /// <summary>
    /// Checks if a filter of type <typeparamref name="TFilter"/> is applied to a reporter of type <typeparamref name="TReporter"/>.
    /// </summary>
    /// <typeparam name="TFilter">The type of the filter.</typeparam>
    /// <typeparam name="TReporter">The type of the reporter.</typeparam>
    /// <param name="controller">The monitoring controller.</param>
    /// <returns>True if the filter is applied; otherwise, false.</returns>
    public static bool IsFilterAppliedToReporter<TFilter, TReporter>(this IMonitoringController controller)
        where TFilter : IMethodFilter
        where TReporter : IMethodCallReporter
    {
        var config = controller.Configuration;
        return config.ComponentRegistry.HasRelationship(typeof(TReporter), typeof(TFilter));
    }

    /// <summary>
    /// Temporarily enables a component of type <typeparamref name="T"/> within a scope.
    /// </summary>
    /// <typeparam name="T">The type of the component to temporarily enable.</typeparam>
    /// <param name="controller">The monitoring controller.</param>
    /// <returns>An <see cref="IDisposable"/> that, when disposed, restores the original state.</returns>
    public static IDisposable TemporarilyEnableComponent<T>(this IMonitoringController controller) where T : IMonitoringComponent
    {
        return new TemporaryComponentState(controller, typeof(T), true);
    }

    /// <summary>
    /// Temporarily disables a component of type <typeparamref name="T"/> within a scope.
    /// </summary>
    /// <typeparam name="T">The type of the component to temporarily disable.</typeparam>
    /// <param name="controller">The monitoring controller.</param>
    /// <returns>An <see cref="IDisposable"/> that, when disposed, restores the original state.</returns>
    public static IDisposable TemporarilyDisableComponent<T>(this IMonitoringController controller) where T : IMonitoringComponent
    {
        return new TemporaryComponentState(controller, typeof(T), false);
    }

    /// <summary>
    /// Validates that the specified type implements <see cref="IMonitoringComponent"/>.
    /// </summary>
    /// <param name="componentType">The component type to validate.</param>
    private static void ValidateComponentType(Type componentType)
    {
        if (componentType is null)
        {
            throw new ArgumentNullException(nameof(componentType));
        }

        if (!typeof(IMonitoringComponent).IsAssignableFrom(componentType))
        {
            throw new ArgumentException($"Type {componentType.FullName} does not implement IMonitoringComponent.", nameof(componentType));
        }
    }

    /// <summary>
    /// A private class used to temporarily change the state of a component.
    /// </summary>
    private sealed class TemporaryComponentState : IDisposable
    {
        private readonly IMonitoringController _controller;
        private readonly Type _componentType;
        private readonly bool _originalState;
        private bool _disposed;

        public TemporaryComponentState(IMonitoringController controller, Type componentType, bool temporaryState)
        {
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            _componentType = componentType ?? throw new ArgumentNullException(nameof(componentType));

            _originalState = controller.GetComponentState(componentType);
            controller.SetComponentState(componentType, temporaryState);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _controller.SetComponentState(_componentType, _originalState);
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Determines whether monitoring should track the current operation based on the version and component states.
    /// </summary>
    /// <param name="controller">The monitoring controller.</param>
    /// <param name="version">The monitoring version.</param>
    /// <param name="componentTypes">Optional component types to check.</param>
    /// <returns>True if the operation should be tracked; otherwise, false.</returns>
    public static bool ShouldTrack(this IMonitoringController controller, MonitoringVersion? version, params Type[] componentTypes)
    {
        if (controller is null)
        {
            throw new ArgumentNullException(nameof(controller));
        }

        if (version is null)
        {
            throw new ArgumentNullException(nameof(version));
        }

        if (!controller.IsEnabled || version != controller.GetCurrentVersion())
        {
            return false;
        }

        foreach (var componentType in componentTypes)
        {
            if (!controller.GetComponentState(componentType))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Asynchronously performs an action if monitoring should track the current operation.
    /// </summary>
    /// <param name="controller">The monitoring controller.</param>
    /// <param name="version">The monitoring version.</param>
    /// <param name="action">The action to perform.</param>
    /// <param name="componentTypes">Optional component types to check.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public static async Task TrackAsync(this IMonitoringController controller, MonitoringVersion version, Func<Task> action, params Type[] componentTypes)
    {
        if (controller.ShouldTrack(version, componentTypes))
        {
            await action().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Synchronously performs an action if monitoring should track the current operation.
    /// </summary>
    /// <param name="controller">The monitoring controller.</param>
    /// <param name="version">The monitoring version.</param>
    /// <param name="action">The action to perform.</param>
    /// <param name="componentTypes">Optional component types to check.</param>
    public static void Track(this IMonitoringController controller, MonitoringVersion version, Action action, params Type[] componentTypes)
    {
        if (controller.ShouldTrack(version, componentTypes))
        {
            action();
        }
    }

    /// <summary>
    /// Checks if a filter of the specified type is enabled for a reporter of the specified type.
    /// </summary>
    /// <param name="filterType">The type of the filter.</param>
    /// <param name="controller">The monitoring controller.</param>
    /// <param name="reporterType">The type of the reporter.</param>
    /// <returns></returns>
    public static bool IsFilterEnabledForReporterType(this IMonitoringController controller, Type reporterType, Type filterType)
    {
        var config = controller.Configuration;

        // Check if the filter and reporter are enabled globally
        var isFilterEnabled = controller.GetComponentState(filterType);
        var isReporterEnabled = controller.GetComponentState(reporterType);

        if (!isFilterEnabled || !isReporterEnabled)
        {
            return false;
        }

        // Check if the relationship exists
        return config.ComponentRegistry.HasRelationship(reporterType, filterType);
    }

    public static void EnableFilter(this IMonitoringController controller, Type filterType)
    {
        controller.SetComponentState(filterType, true);
    }

    public static void DisableFilter(this IMonitoringController controller, Type filterType)
    {
        controller.SetComponentState(filterType, false);
    }

    public static void EnableReporter(this IMonitoringController controller, Type reporterType)
    {
        controller.SetComponentState(reporterType, true);
    }

    public static void DisableReporter(this IMonitoringController controller, Type reporterType)
    {
        controller.SetComponentState(reporterType, false);
    }

    public static void EnableFilterForReporter(this IMonitoringController controller, Type filterType, Type reporterType)
    {
        var config = controller.Configuration;
        config.ComponentRegistry.AddRelationship(reporterType, filterType);
    }

    public static void DisableFilterForReporter(this IMonitoringController controller, Type filterType, Type reporterType)
    {
        var config = controller.Configuration;
        config.ComponentRegistry.RemoveRelationship(reporterType, filterType);
    }

    public static void EnableFilterForReporter<TFilter, TReporter>(this IMonitoringController controller)
        where TFilter : IMethodFilter
        where TReporter : IMethodCallReporter
    {
        controller.EnableFilterForReporter(typeof(TFilter), typeof(TReporter));
    }

    public static void DisableFilterForReporter<TFilter, TReporter>(this IMonitoringController controller)
        where TFilter : IMethodFilter
        where TReporter : IMethodCallReporter
    {
        controller.DisableFilterForReporter(typeof(TFilter), typeof(TReporter));
    }

    public static void EnableReporter<TReporter>(this IMonitoringController controller) where TReporter : IMethodCallReporter
    {
        controller.EnableReporter(typeof(TReporter));
    }

    public static void DisableReporter<TReporter>(this IMonitoringController controller) where TReporter : IMethodCallReporter
    {
        controller.DisableReporter(typeof(TReporter));
    }

    public static void EnableFilter<TFilter>(this IMonitoringController controller) where TFilter : IMethodFilter
    {
        controller.EnableFilter(typeof(TFilter));
    }

    public static void DisableFilter<TFilter>(this IMonitoringController controller) where TFilter : IMethodFilter
    {
        controller.DisableFilter(typeof(TFilter));
    }

    public static bool IsFilterEnabledForReporter<TReporter, TFilter>(this IMonitoringController controller)
        where TReporter : IMethodCallReporter
        where TFilter : IMethodFilter
    {
        return controller.IsFilterEnabledForReporterType(typeof(TReporter), typeof(TFilter));
    }

    public static bool IsFilterEnabledForReporter(this IMonitoringController controller, Type filterType, Type reporterType)
    {
        return controller.IsFilterEnabledForReporterType(reporterType, filterType);
    }

    public static bool IsReporterEnabled<TReporter>(this IMonitoringController controller) where TReporter : IMethodCallReporter
    {
        return controller.GetComponentState(typeof(TReporter));
    }

    public static bool IsReporterEnabled(this IMonitoringController controller, Type type)
    {
        return controller.GetComponentState(type);
    }

    public static bool IsFilterEnabled<TFilter>(this IMonitoringController controller) where TFilter : IMethodFilter
    {
        return controller.GetComponentState(typeof(TFilter));
    }
    public static bool IsFilterEnabled(this IMonitoringController controller, Type type)
    {
        return controller.GetComponentState(type);
    }

    public static void EnableOutput<TOutput>(this IMonitoringController controller) where TOutput : IReportOutput
    {
        controller.SetComponentState(typeof(TOutput), true);
    }

    public static void DisableOutput<TOutput>(this IMonitoringController controller) where TOutput : IReportOutput
    {
        controller.SetComponentState(typeof(TOutput), false);
    }

    public static bool IsOutputEnabled<TOutput>(this IMonitoringController controller) where TOutput : IReportOutput
    {
        return controller.GetComponentState(typeof(TOutput));
    }

    public static void EnableOutput(this IMonitoringController controller, Type outputType)
    {
        controller.SetComponentState(outputType, true);
    }

    public static void DisableOutput(this IMonitoringController controller, Type outputType)
    {
        controller.SetComponentState(outputType, false);
    }

    public static bool IsOutputEnabled(this IMonitoringController controller, Type outputType)
    {
        return controller.GetComponentState(outputType);
    }
}

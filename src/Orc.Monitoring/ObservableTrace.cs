namespace Orc.Monitoring;

using System.Reactive.Linq;
using System;
using System.Threading;
using Catel.Logging;

public static class ObservableTrace
{
    private static readonly ILog Log = LogManager.GetCurrentClassLogger();
    private static int SubscriptionIdCounter = 0;

    public static IObservable<TSource> Trace<TSource>(this IObservable<TSource> source, string traceName)
    {
        return Observable.Create<TSource>(observer =>
        {
            var currentSubscriptionId = Interlocked.Increment(ref SubscriptionIdCounter);
            void LogTraceEvent(string message, object? value) => Log.Debug($"{traceName}{currentSubscriptionId}: {message}({value ?? string.Empty})");

            LogTraceEvent("Subscribe", string.Empty);

            var disposable = source.Subscribe(
                onNext: value => { LogTraceEvent("OnNext", value); observer.OnNext(value); },
                onError: error => { LogTraceEvent("OnError", error.ToString()); observer.OnError(error); },
                onCompleted: () => { LogTraceEvent("OnCompleted", string.Empty); observer.OnCompleted(); });

            return () => { LogTraceEvent("Dispose", string.Empty); disposable.Dispose(); };
        });
    }
}

namespace Orc.Monitoring;

using System;
using System.Threading;


public class VersionManager
{
    private long _lastTimestamp;
    private int _counter;

    public MonitoringVersion GetNextVersion()
    {
        long timestamp;
        int counter;

        while (true)
        {
            long currentTimestamp = GetTimestamp();
            long lastTimestamp = Interlocked.Read(ref _lastTimestamp);

            if (currentTimestamp > lastTimestamp)
            {
                if (Interlocked.CompareExchange(ref _lastTimestamp, currentTimestamp, lastTimestamp) == lastTimestamp)
                {
                    timestamp = currentTimestamp;
                    counter = Interlocked.Exchange(ref _counter, 0);
                    break;
                }
            }
            else
            {
                counter = Interlocked.Increment(ref _counter);
                timestamp = lastTimestamp;
                break;
            }
        }

        return new MonitoringVersion(timestamp, counter, Guid.NewGuid());
    }

    private static long GetTimestamp() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}

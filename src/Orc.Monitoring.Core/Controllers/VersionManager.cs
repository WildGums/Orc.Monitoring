namespace Orc.Monitoring.Core.Controllers;

using System;
using Models;

public class VersionManager
{
    private long _lastTimestamp;
    private int _counter;
    private readonly object _lock = new();

    public MonitoringVersion GetNextVersion()
    {
        lock (_lock)
        {
            var currentTimestamp = GetTimestamp();
            if (currentTimestamp > _lastTimestamp)
            {
                _lastTimestamp = currentTimestamp;
                _counter = 0;
            }
            else
            {
                _counter++;
                // If counter overflows, force timestamp to increment
                if (_counter < 0)
                {
                    _lastTimestamp++;
                    _counter = 0;
                }
            }

            return new MonitoringVersion(_lastTimestamp, _counter, Guid.NewGuid());
        }
    }

    private static long GetTimestamp() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}

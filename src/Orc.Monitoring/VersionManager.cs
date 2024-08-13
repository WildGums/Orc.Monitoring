﻿namespace Orc.Monitoring;

using System;
using System.Threading;


public class VersionManager
{
    private long _lastTimestamp;
    private int _counter;
    private readonly object _lock = new object();

    public MonitoringVersion GetNextVersion()
    {
        lock (_lock)
        {
            var now = GetTimestamp();
            if (now > _lastTimestamp)
            {
                _lastTimestamp = now;
                _counter = 0;
            }
            else
            {
                _counter++;
            }
            return new MonitoringVersion(_lastTimestamp, _counter, Guid.NewGuid());
        }
    }

    private static long GetTimestamp() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}

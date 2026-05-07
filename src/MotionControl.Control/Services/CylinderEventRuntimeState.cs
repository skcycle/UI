namespace MotionControl.Control.Services;

public sealed class CylinderEventRuntimeState
{
    private readonly object _syncLock = new();
    private readonly IEventLogStore _eventLogStore;
    private CylinderEventRecord[] _recentEvents = Array.Empty<CylinderEventRecord>();

    public event Action? EventsChanged;

    public CylinderEventRuntimeState() : this(null!) { }

    public CylinderEventRuntimeState(IEventLogStore eventLogStore)
    {
        _eventLogStore = eventLogStore;
    }

    public IReadOnlyList<CylinderEventRecord> RecentEvents
    {
        get
        {
            lock (_syncLock) { return _recentEvents; }
        }
    }

    public void Add(CylinderEventRecord record)
    {
        lock (_syncLock)
        {
            var newList = new CylinderEventRecord[Math.Min(_recentEvents.Length + 1, 200)];
            var startIndex = _recentEvents.Length >= 200 ? 1 : 0;
            if (_recentEvents.Length >= 200)
                Array.Copy(_recentEvents, 1, newList, 0, 199);
            else
                Array.Copy(_recentEvents, 0, newList, 0, _recentEvents.Length);
            newList[Math.Min(_recentEvents.Length, 199)] = record;
            _recentEvents = newList;
        }
        EventsChanged?.Invoke();

        _eventLogStore?.Enqueue(new RuntimeEventLogEntry
        {
            TimestampUtc = record.Timestamp,
            Module = "Cylinder",
            EventType = record.EventType,
            Level = record.EventType.Contains("FAIL", StringComparison.OrdinalIgnoreCase)
                 || record.EventType.Contains("ERROR", StringComparison.OrdinalIgnoreCase)
                 || record.EventType.Contains("ALARM", StringComparison.OrdinalIgnoreCase)
                    ? "Error" : "Info",
            ObjectName = record.CylinderName,
            Message = record.Message,
        });
    }
}

public sealed class CylinderEventRecord
{
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string CylinderName { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}

namespace MotionControl.Control.Services;

public sealed class WorkHeadEventRuntimeState
{
    private readonly object _syncLock = new();
    private readonly IEventLogStore _eventLogStore;
    private WorkHeadEventRecord[] _recentEvents = Array.Empty<WorkHeadEventRecord>();

    public event Action? EventsChanged;

    public WorkHeadEventRuntimeState() : this(null!) { }

    public WorkHeadEventRuntimeState(IEventLogStore eventLogStore)
    {
        _eventLogStore = eventLogStore;
    }

    public IReadOnlyList<WorkHeadEventRecord> RecentEvents
    {
        get
        {
            lock (_syncLock) { return _recentEvents; }
        }
    }

    public void Add(WorkHeadEventRecord record)
    {
        lock (_syncLock)
        {
            var newList = new WorkHeadEventRecord[Math.Min(_recentEvents.Length + 1, 200)];
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
            Module = "WorkHead",
            EventType = record.EventType,
            Level = "Info",
            ObjectName = record.WorkHeadName,
            Message = record.Message,
        });
    }
}

public sealed class WorkHeadEventRecord
{
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string WorkHeadName { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}

public sealed class PositionSetupEventRuntimeState
{
    private readonly object _syncLock = new();
    private readonly IEventLogStore _eventLogStore;
    private PositionSetupEventRecord[] _recentEvents = Array.Empty<PositionSetupEventRecord>();

    public event Action? EventsChanged;

    public PositionSetupEventRuntimeState() : this(null!) { }

    public PositionSetupEventRuntimeState(IEventLogStore eventLogStore)
    {
        _eventLogStore = eventLogStore;
    }

    public IReadOnlyList<PositionSetupEventRecord> RecentEvents
    {
        get
        {
            lock (_syncLock) { return _recentEvents; }
        }
    }

    public void Add(PositionSetupEventRecord record)
    {
        lock (_syncLock)
        {
            var newList = new PositionSetupEventRecord[Math.Min(_recentEvents.Length + 1, 200)];
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
            Module = "PositionSetup",
            EventType = record.EventType,
            Level = "Info",
            ObjectName = record.PositionName,
            Message = record.Message,
        });
    }
}

public sealed class PositionSetupEventRecord
{
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string PositionName { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}

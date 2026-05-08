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
            Level = RuntimeEventLogEntry.DetermineLevel(null, record.EventType),
            ObjectName = record.WorkHeadName,
            Message = record.Message,
            PayloadJson = PayloadBuilder.WorkHead(
                record.Action,
                record.WorkHeadName,
                record.TargetX,
                record.TargetY,
                record.TargetZ,
                record.TargetR,
                record.Velocity,
                result: record.Result,
                record.DurationMs)
        });
    }
}

public sealed class WorkHeadEventRecord
{
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string WorkHeadName { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;

    // Optional structured fields for PayloadJson
    public string Action { get; init; } = string.Empty;
    public double? TargetX { get; init; }
    public double? TargetY { get; init; }
    public double? TargetZ { get; init; }
    public double? TargetR { get; init; }
    public double? Velocity { get; init; }
    public string? Result { get; init; }
    public int? DurationMs { get; init; }
}



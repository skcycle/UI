namespace MotionControl.Control.Services;

public sealed class MagazineEventRuntimeState
{
    private readonly object _syncLock = new();
    private readonly IEventLogStore _eventLogStore;
    private MagazineEventRecord[] _recentEvents = Array.Empty<MagazineEventRecord>();

    public event Action? EventsChanged;

    public MagazineEventRuntimeState() : this(null!) { }

    public MagazineEventRuntimeState(IEventLogStore eventLogStore)
    {
        _eventLogStore = eventLogStore;
    }

    public IReadOnlyList<MagazineEventRecord> RecentEvents
    {
        get
        {
            lock (_syncLock) { return _recentEvents; }
        }
    }

    public void Add(MagazineEventRecord record)
    {
        lock (_syncLock)
        {
            var newList = new MagazineEventRecord[Math.Min(_recentEvents.Length + 1, 200)];
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
            Module = "Magazine",
            EventType = record.EventType,
            Level = RuntimeEventLogEntry.DetermineLevel(null, record.EventType),
            ObjectName = record.MagazineName,
            Message = record.Message,
        });
    }
}

public sealed class MagazineEventRecord
{
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string MagazineName { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}

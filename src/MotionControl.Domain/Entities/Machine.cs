using MotionControl.Domain.Enums;

namespace MotionControl.Domain.Entities;

public sealed class Machine
{
    private readonly object _collectionsLock = new();
    private readonly List<Axis> _axes;
    private readonly List<IoPoint> _ioPoints;
    private readonly List<Cylinder> _cylinders;
    private readonly List<WorkHead> _workHeads;
    private readonly List<Magazine> _magazines;
    private readonly List<Alarm> _alarms;

    public Machine(
        IReadOnlyCollection<Axis> axes,
        IReadOnlyCollection<AxisGroup> groups,
        IReadOnlyCollection<IoPoint>? ioPoints = null,
        IReadOnlyCollection<Cylinder>? cylinders = null,
        IReadOnlyCollection<WorkHead>? workHeads = null,
        IReadOnlyCollection<Magazine>? magazines = null,
        IReadOnlyCollection<Alarm>? alarms = null)
    {
        _axes = axes.ToList();
        Groups = groups;
        _ioPoints = ioPoints?.ToList() ?? new List<IoPoint>();
        _cylinders = cylinders?.ToList() ?? new List<Cylinder>();
        _workHeads = workHeads?.ToList() ?? new List<WorkHead>();
        _magazines = magazines?.ToList() ?? new List<Magazine>();
        _alarms = alarms?.ToList() ?? new List<Alarm>();
    }

    /// <summary>
    /// 返回集合快照，避免轮询线程迭代时被 UI 线程修改导致 Collection was modified。
    /// </summary>
    public IReadOnlyCollection<Axis> Axes
    {
        get { lock (_collectionsLock) { return _axes.ToList(); } }
    }

    public IReadOnlyCollection<AxisGroup> Groups { get; }

    public IReadOnlyCollection<IoPoint> IoPoints
    {
        get { lock (_collectionsLock) { return _ioPoints.ToList(); } }
    }

    public IReadOnlyCollection<Cylinder> Cylinders
    {
        get { lock (_collectionsLock) { return _cylinders.ToList(); } }
    }

    public IReadOnlyCollection<WorkHead> WorkHeads
    {
        get { lock (_collectionsLock) { return _workHeads.ToList(); } }
    }

    public IReadOnlyCollection<Magazine> Magazines
    {
        get { lock (_collectionsLock) { return _magazines.ToList(); } }
    }

    public IReadOnlyCollection<Alarm> Alarms
    {
        get { lock (_collectionsLock) { return _alarms.ToList(); } }
    }

    public SystemState CurrentState { get; private set; } = SystemState.Initializing;
    public bool IsConnected { get; private set; }

    public void SetSystemState(SystemState state) => CurrentState = state;

    public void SetConnected(bool connected) => IsConnected = connected;

    public void AddAxis(Axis axis)
    {
        lock (_collectionsLock)
        {
            if (_axes.Any(existing => existing.Id.Value == axis.Id.Value))
                return;
            _axes.Add(axis);
        }
    }

    public bool RemoveAxis(int axisNo)
    {
        lock (_collectionsLock)
        {
            var axis = _axes.FirstOrDefault(item => item.Id.Value == axisNo);
            if (axis is null) return false;
            _axes.Remove(axis);
            return true;
        }
    }

    public void AddIoPoint(IoPoint ioPoint)
    {
        lock (_collectionsLock)
        {
            if (_ioPoints.Any(existing => existing.IsOutput == ioPoint.IsOutput && existing.Address == ioPoint.Address))
                return;
            _ioPoints.Add(ioPoint);
        }
    }

    public bool RemoveIoPoint(bool isOutput, int address)
    {
        lock (_collectionsLock)
        {
            var ioPoint = _ioPoints.FirstOrDefault(item => item.IsOutput == isOutput && item.Address == address);
            if (ioPoint is null) return false;
            _ioPoints.Remove(ioPoint);
            return true;
        }
    }

    public void AddCylinder(Cylinder cylinder)
    {
        lock (_collectionsLock)
        {
            if (_cylinders.Any(existing => string.Equals(existing.Name, cylinder.Name, StringComparison.OrdinalIgnoreCase)))
                return;
            _cylinders.Add(cylinder);
        }
    }

    public bool RemoveCylinder(string name)
    {
        lock (_collectionsLock)
        {
            var cylinder = _cylinders.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
            if (cylinder is null) return false;
            _cylinders.Remove(cylinder);
            return true;
        }
    }

    public void AddWorkHead(WorkHead workHead)
    {
        lock (_collectionsLock)
        {
            if (_workHeads.Any(existing => string.Equals(existing.Name, workHead.Name, StringComparison.OrdinalIgnoreCase)))
                return;
            _workHeads.Add(workHead);
        }
    }

    public bool RemoveWorkHead(string name)
    {
        lock (_collectionsLock)
        {
            var workHead = _workHeads.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
            if (workHead is null) return false;
            _workHeads.Remove(workHead);
            return true;
        }
    }

    public void AddMagazine(Magazine magazine)
    {
        lock (_collectionsLock)
        {
            if (_magazines.Any(existing => string.Equals(existing.Name, magazine.Name, StringComparison.OrdinalIgnoreCase)))
                return;
            _magazines.Add(magazine);
        }
    }

    public bool RemoveMagazine(string name)
    {
        lock (_collectionsLock)
        {
            var magazine = _magazines.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
            if (magazine is null) return false;
            _magazines.Remove(magazine);
            return true;
        }
    }

    public bool UpsertAlarm(string code, string message, string source = "System", string category = "General", string severity = "Error")
    {
        lock (_collectionsLock)
        {
            var now = DateTime.Now;
            var existing = _alarms.FirstOrDefault(alarm => alarm.Code == code && alarm.IsActive);
            if (existing is not null)
            {
                existing.Update(message, now, source, category, severity);
                return false;
            }

            _alarms.RemoveAll(alarm => alarm.Code == code && !alarm.IsActive);
            _alarms.Add(new Alarm(code, message, now, source, category, severity));
            return true;
        }
    }

    public bool ClearAlarm(string code)
    {
        lock (_collectionsLock)
        {
            var existing = _alarms.FirstOrDefault(alarm => alarm.Code == code && alarm.IsActive);
            if (existing is null) return false;
            existing.Clear();
            return true;
        }
    }
}

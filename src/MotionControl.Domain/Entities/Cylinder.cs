using MotionControl.Domain.Enums;

namespace MotionControl.Domain.Entities;

public enum CylinderCommandType
{
    None,
    Extend,
    Retract
}

public sealed class Cylinder
{
    private readonly object _stateLock = new();

    public Cylinder(string name, int extendSensorInputAddress, int retractSensorInputAddress, int extendOutputAddress, int retractOutputAddress, string description = "", int actionTimeoutMs = 3000)
    {
        _name = name;
        _extendSensorInputAddress = extendSensorInputAddress;
        _retractSensorInputAddress = retractSensorInputAddress;
        _extendOutputAddress = extendOutputAddress;
        _retractOutputAddress = retractOutputAddress;
        _description = description;
        _actionTimeoutMs = actionTimeoutMs;
    }

    private string _name;
    public string Name
    {
        get { lock (_stateLock) return _name; }
        private set { lock (_stateLock) _name = value; }
    }

    private string _description;
    public string Description
    {
        get { lock (_stateLock) return _description; }
        private set { lock (_stateLock) _description = value; }
    }

    private int _extendSensorInputAddress;
    public int ExtendSensorInputAddress
    {
        get { lock (_stateLock) return _extendSensorInputAddress; }
        private set { lock (_stateLock) _extendSensorInputAddress = value; }
    }

    private int _retractSensorInputAddress;
    public int RetractSensorInputAddress
    {
        get { lock (_stateLock) return _retractSensorInputAddress; }
        private set { lock (_stateLock) _retractSensorInputAddress = value; }
    }

    private int _extendOutputAddress;
    public int ExtendOutputAddress
    {
        get { lock (_stateLock) return _extendOutputAddress; }
        private set { lock (_stateLock) _extendOutputAddress = value; }
    }

    private int _retractOutputAddress;
    public int RetractOutputAddress
    {
        get { lock (_stateLock) return _retractOutputAddress; }
        private set { lock (_stateLock) _retractOutputAddress = value; }
    }

    private int _actionTimeoutMs;
    public int ActionTimeoutMs
    {
        get { lock (_stateLock) return _actionTimeoutMs; }
        private set { lock (_stateLock) _actionTimeoutMs = value; }
    }

    private CylinderState _state = CylinderState.Unknown;
    public CylinderState State
    {
        get { lock (_stateLock) return _state; }
        private set { lock (_stateLock) _state = value; }
    }

    private CylinderCommandType _pendingCommand = CylinderCommandType.None;
    public CylinderCommandType PendingCommand
    {
        get { lock (_stateLock) return _pendingCommand; }
        private set { lock (_stateLock) _pendingCommand = value; }
    }

    private DateTime? _lastCommandStartedAtUtc;
    public DateTime? LastCommandStartedAtUtc
    {
        get { lock (_stateLock) return _lastCommandStartedAtUtc; }
        private set { lock (_stateLock) _lastCommandStartedAtUtc = value; }
    }

    public void UpdateMetadata(string name, int extendSensorInputAddress, int retractSensorInputAddress, int extendOutputAddress, int retractOutputAddress, string description, int actionTimeoutMs = 3000)
    {
        lock (_stateLock)
        {
            _name = name;
            _extendSensorInputAddress = extendSensorInputAddress;
            _retractSensorInputAddress = retractSensorInputAddress;
            _extendOutputAddress = extendOutputAddress;
            _retractOutputAddress = retractOutputAddress;
            _description = description;
            _actionTimeoutMs = actionTimeoutMs;
        }
    }

    /// <summary>轮询线程更新气缸状态（锁内执行完整判定逻辑）。</summary>
    public void UpdateState(bool extendSensorOn, bool retractSensorOn, bool extendOutputOn, bool retractOutputOn)
    {
        lock (_stateLock)
        {
            UpdateStateLocked(extendSensorOn, retractSensorOn, extendOutputOn, retractOutputOn);
        }
    }

    private void UpdateStateLocked(bool extendSensorOn, bool retractSensorOn, bool extendOutputOn, bool retractOutputOn)
    {
        var hasExtendSensor = _extendSensorInputAddress >= 0;
        var hasRetractSensor = _retractSensorInputAddress >= 0;

        if (hasExtendSensor && hasRetractSensor)
        {
            if (extendSensorOn && retractSensorOn) { _state = CylinderState.Conflict; return; }
            if (extendOutputOn && retractOutputOn) { _state = CylinderState.Conflict; return; }
            if (!extendOutputOn && !retractOutputOn && (extendSensorOn || retractSensorOn)) { _state = CylinderState.Conflict; return; }

            if (extendSensorOn) { _state = CylinderState.Extended; ClearPendingCommandLocked(); return; }
            if (retractSensorOn) { _state = CylinderState.Retracted; ClearPendingCommandLocked(); return; }
            if (extendOutputOn) { _state = CylinderState.Extending; return; }
            if (retractOutputOn) { _state = CylinderState.Retracting; return; }
            _state = CylinderState.Unknown;
            return;
        }

        if (hasExtendSensor && !hasRetractSensor)
        {
            if (extendSensorOn && !extendOutputOn) { _state = CylinderState.Conflict; return; }
            if (extendSensorOn) { _state = CylinderState.Extended; ClearPendingCommandLocked(); return; }
            if (extendOutputOn) { _state = CylinderState.Extending; return; }
            _state = CylinderState.Retracted;
            if (_pendingCommand == CylinderCommandType.Extend) ClearPendingCommandLocked();
            return;
        }

        if (!hasExtendSensor && hasRetractSensor)
        {
            if (retractSensorOn && !retractOutputOn) { _state = CylinderState.Conflict; return; }
            if (retractSensorOn) { _state = CylinderState.Retracted; ClearPendingCommandLocked(); return; }
            if (retractOutputOn) { _state = CylinderState.Retracting; return; }
            _state = CylinderState.Extended;
            if (_pendingCommand == CylinderCommandType.Retract) ClearPendingCommandLocked();
            return;
        }

        _state = CylinderState.Unknown;
    }

    public void StartExtendCommand()
    {
        lock (_stateLock)
        {
            _pendingCommand = CylinderCommandType.Extend;
            _lastCommandStartedAtUtc = DateTime.UtcNow;
        }
    }

    public void StartRetractCommand()
    {
        lock (_stateLock)
        {
            _pendingCommand = CylinderCommandType.Retract;
            _lastCommandStartedAtUtc = DateTime.UtcNow;
        }
    }

    public void ClearPendingCommand()
    {
        lock (_stateLock)
        {
            _pendingCommand = CylinderCommandType.None;
            _lastCommandStartedAtUtc = null;
        }
    }

    private void ClearPendingCommandLocked()
    {
        _pendingCommand = CylinderCommandType.None;
        _lastCommandStartedAtUtc = null;
    }

    public bool IsActionTimedOut(DateTime utcNow)
    {
        lock (_stateLock)
        {
            if (_pendingCommand == CylinderCommandType.None || _lastCommandStartedAtUtc is null)
                return false;
            return utcNow - _lastCommandStartedAtUtc.Value >= TimeSpan.FromMilliseconds(_actionTimeoutMs);
        }
    }
}

using MotionControl.Domain.Enums;
using MotionControl.Domain.ValueObjects;

namespace MotionControl.Domain.Entities;

public sealed class Axis
{
    private readonly object _stateLock = new();

    public Axis(AxisId id, string name, int controllerAxisNo)
    {
        Id = id;
        Name = name;
        ControllerAxisNo = controllerAxisNo;
    }

    public AxisId Id { get; }
    public int ControllerAxisNo { get; }

    public string Name
    {
        get { lock (_stateLock) return _name; }
        private set { lock (_stateLock) _name = value; }
    }
    private string _name = string.Empty;

    public AxisState State
    {
        get { lock (_stateLock) return _state; }
        private set { lock (_stateLock) _state = value; }
    }
    private AxisState _state = AxisState.Disabled;

    public ServoState ServoState
    {
        get { lock (_stateLock) return _servoState; }
        private set { lock (_stateLock) _servoState = value; }
    }
    private ServoState _servoState = ServoState.Off;

    public MotionMode MotionMode
    {
        get { lock (_stateLock) return _motionMode; }
        private set { lock (_stateLock) _motionMode = value; }
    }
    private MotionMode _motionMode = MotionMode.None;

    public double CurrentPosition
    {
        get { lock (_stateLock) return _currentPosition; }
        private set { lock (_stateLock) _currentPosition = value; }
    }
    private double _currentPosition;

    public double EncoderPosition
    {
        get { lock (_stateLock) return _encoderPosition; }
        private set { lock (_stateLock) _encoderPosition = value; }
    }
    private double _encoderPosition;

    public double CurrentVelocity
    {
        get { lock (_stateLock) return _currentVelocity; }
        private set { lock (_stateLock) _currentVelocity = value; }
    }
    private double _currentVelocity;

    public double TargetPosition
    {
        get { lock (_stateLock) return _targetPosition; }
        private set { lock (_stateLock) _targetPosition = value; }
    }
    private double _targetPosition;

    public bool IsHomed
    {
        get { lock (_stateLock) return _isHomed; }
        private set { lock (_stateLock) _isHomed = value; }
    }
    private bool _isHomed;

    public bool HasAlarm
    {
        get { lock (_stateLock) return _hasAlarm; }
        private set { lock (_stateLock) _hasAlarm = value; }
    }
    private bool _hasAlarm;

    public bool PositiveLimitTriggered
    {
        get { lock (_stateLock) return _positiveLimitTriggered; }
        private set { lock (_stateLock) _positiveLimitTriggered = value; }
    }
    private bool _positiveLimitTriggered;

    public bool NegativeLimitTriggered
    {
        get { lock (_stateLock) return _negativeLimitTriggered; }
        private set { lock (_stateLock) _negativeLimitTriggered = value; }
    }
    private bool _negativeLimitTriggered;

    public bool PositiveSoftLimitTriggered
    {
        get { lock (_stateLock) return _positiveSoftLimitTriggered; }
        private set { lock (_stateLock) _positiveSoftLimitTriggered = value; }
    }
    private bool _positiveSoftLimitTriggered;

    public bool NegativeSoftLimitTriggered
    {
        get { lock (_stateLock) return _negativeSoftLimitTriggered; }
        private set { lock (_stateLock) _negativeSoftLimitTriggered = value; }
    }
    private bool _negativeSoftLimitTriggered;

    public SoftLimit? SoftLimit
    {
        get { lock (_stateLock) return _softLimit; }
        private set { lock (_stateLock) _softLimit = value; }
    }
    private SoftLimit? _softLimit;

    public double WorkVelocity
    {
        get { lock (_stateLock) return _workVelocity; }
        private set { lock (_stateLock) _workVelocity = value; }
    }
    private double _workVelocity = 200;

    public double SetupVelocity
    {
        get { lock (_stateLock) return _setupVelocity; }
        private set { lock (_stateLock) _setupVelocity = value; }
    }
    private double _setupVelocity = 50;

    public double Acceleration
    {
        get { lock (_stateLock) return _acceleration; }
        private set { lock (_stateLock) _acceleration = value; }
    }
    private double _acceleration = 100;

    public double Deceleration
    {
        get { lock (_stateLock) return _deceleration; }
        private set { lock (_stateLock) _deceleration = value; }
    }
    private double _deceleration = 100;

    public double PulseEquivalent
    {
        get { lock (_stateLock) return _pulseEquivalent; }
        private set { lock (_stateLock) _pulseEquivalent = value; }
    }
    private double _pulseEquivalent = 1000;

    public HomeMode HomeMode
    {
        get { lock (_stateLock) return _homeMode; }
        private set { lock (_stateLock) _homeMode = value; }
    }
    private HomeMode _homeMode = HomeMode.Default;

    public string ServoBinding
    {
        get { lock (_stateLock) return _servoBinding; }
        private set { lock (_stateLock) _servoBinding = value; }
    }
    private string _servoBinding = string.Empty;

    /// <summary>轮询线程批量更新反馈（锁内一次性完成，保证快照一致性）。</summary>
    public void UpdateFeedback(
        double currentPosition,
        double encoderPosition,
        double currentVelocity,
        AxisState state,
        ServoState servoState,
        bool hasAlarm,
        bool isHomed,
        bool positiveLimitTriggered,
        bool negativeLimitTriggered,
        bool positiveSoftLimitTriggered,
        bool negativeSoftLimitTriggered)
    {
        lock (_stateLock)
        {
            _currentPosition = currentPosition;
            _encoderPosition = encoderPosition;
            _currentVelocity = currentVelocity;
            _state = state;
            _servoState = servoState;
            _hasAlarm = hasAlarm;
            _isHomed = isHomed;
            _positiveLimitTriggered = positiveLimitTriggered;
            _negativeLimitTriggered = negativeLimitTriggered;
            _positiveSoftLimitTriggered = positiveSoftLimitTriggered;
            _negativeSoftLimitTriggered = negativeSoftLimitTriggered;
        }
    }

    public void SetTargetPosition(double targetPosition, MotionMode motionMode)
    {
        lock (_stateLock)
        {
            _targetPosition = targetPosition;
            _motionMode = motionMode;
        }
    }

    public void ApplyState(AxisState state)
    {
        lock (_stateLock) { _state = state; }
    }

    public void MarkHomed()
    {
        lock (_stateLock) { _isHomed = true; }
    }

    public void ClearHomed()
    {
        lock (_stateLock) { _isHomed = false; }
    }

    public void SetAlarm()
    {
        lock (_stateLock) { _hasAlarm = true; }
    }

    public void ClearAlarm()
    {
        lock (_stateLock) { _hasAlarm = false; }
    }

    public void SetSoftLimit(SoftLimit softLimit)
    {
        lock (_stateLock) { _softLimit = softLimit; }
    }

    public void SetWorkVelocity(double workVelocity)
    {
        lock (_stateLock) { _workVelocity = workVelocity; }
    }

    public void SetSetupVelocity(double setupVelocity)
    {
        lock (_stateLock) { _setupVelocity = setupVelocity; }
    }

    public void SetPulseEquivalent(double pulseEquivalent)
    {
        lock (_stateLock) { _pulseEquivalent = pulseEquivalent <= 0 ? 1000 : pulseEquivalent; }
    }

    public void SetAcceleration(double acceleration)
    {
        lock (_stateLock) { _acceleration = acceleration <= 0 ? 100 : acceleration; }
    }

    public void SetDeceleration(double deceleration)
    {
        lock (_stateLock) { _deceleration = deceleration <= 0 ? 100 : deceleration; }
    }

    public void SetHomeMode(HomeMode homeMode)
    {
        lock (_stateLock) { _homeMode = homeMode; }
    }

    public void SetServoBinding(string servoBinding)
    {
        lock (_stateLock) { _servoBinding = servoBinding; }
    }

    public void SetName(string name)
    {
        lock (_stateLock) { _name = name; }
    }
}

using System.Collections.ObjectModel;

namespace MotionControl.Domain.Entities;

public sealed class WorkHead
{
    private readonly object _stateLock = new();

    public WorkHead(string name, string description, int xAxisNo, int yAxisNo, int zAxisNo, int rAxisNo, int vacuumOutputAddress, int blowOutputAddress, int vacuumInputAddress, int generalOutputAddress1, int generalOutputAddress2, int generalInputAddress1, int generalInputAddress2, int vacuumTimeoutMs = 3000, IEnumerable<WorkHeadPosition>? positions = null, double safeZ = 0)
    {
        _name = name;
        _description = description;
        _xAxisNo = xAxisNo;
        _yAxisNo = yAxisNo;
        _zAxisNo = zAxisNo;
        _rAxisNo = rAxisNo;
        _vacuumOutputAddress = vacuumOutputAddress;
        _blowOutputAddress = blowOutputAddress;
        _vacuumInputAddress = vacuumInputAddress;
        _generalOutputAddress1 = generalOutputAddress1;
        _generalOutputAddress2 = generalOutputAddress2;
        _generalInputAddress1 = generalInputAddress1;
        _generalInputAddress2 = generalInputAddress2;
        _vacuumTimeoutMs = vacuumTimeoutMs;
        Positions = positions is null ? new ObservableCollection<WorkHeadPosition>() : new ObservableCollection<WorkHeadPosition>(positions);
        _safeZ = safeZ;
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

    private int _xAxisNo;
    public int XAxisNo
    {
        get { lock (_stateLock) return _xAxisNo; }
        private set { lock (_stateLock) _xAxisNo = value; }
    }

    private int _yAxisNo;
    public int YAxisNo
    {
        get { lock (_stateLock) return _yAxisNo; }
        private set { lock (_stateLock) _yAxisNo = value; }
    }

    private int _zAxisNo;
    public int ZAxisNo
    {
        get { lock (_stateLock) return _zAxisNo; }
        private set { lock (_stateLock) _zAxisNo = value; }
    }

    private int _rAxisNo;
    public int RAxisNo
    {
        get { lock (_stateLock) return _rAxisNo; }
        private set { lock (_stateLock) _rAxisNo = value; }
    }

    private int _vacuumOutputAddress;
    public int VacuumOutputAddress
    {
        get { lock (_stateLock) return _vacuumOutputAddress; }
        private set { lock (_stateLock) _vacuumOutputAddress = value; }
    }

    private int _blowOutputAddress;
    public int BlowOutputAddress
    {
        get { lock (_stateLock) return _blowOutputAddress; }
        private set { lock (_stateLock) _blowOutputAddress = value; }
    }

    private int _vacuumInputAddress;
    public int VacuumInputAddress
    {
        get { lock (_stateLock) return _vacuumInputAddress; }
        private set { lock (_stateLock) _vacuumInputAddress = value; }
    }

    private int _generalOutputAddress1;
    public int GeneralOutputAddress1
    {
        get { lock (_stateLock) return _generalOutputAddress1; }
        private set { lock (_stateLock) _generalOutputAddress1 = value; }
    }

    private int _generalOutputAddress2;
    public int GeneralOutputAddress2
    {
        get { lock (_stateLock) return _generalOutputAddress2; }
        private set { lock (_stateLock) _generalOutputAddress2 = value; }
    }

    private int _generalInputAddress1;
    public int GeneralInputAddress1
    {
        get { lock (_stateLock) return _generalInputAddress1; }
        private set { lock (_stateLock) _generalInputAddress1 = value; }
    }

    private int _generalInputAddress2;
    public int GeneralInputAddress2
    {
        get { lock (_stateLock) return _generalInputAddress2; }
        private set { lock (_stateLock) _generalInputAddress2 = value; }
    }

    private int _vacuumTimeoutMs;
    public int VacuumTimeoutMs
    {
        get { lock (_stateLock) return _vacuumTimeoutMs; }
        private set { lock (_stateLock) _vacuumTimeoutMs = value; }
    }

    private double _safeZ;
    public double SafeZ
    {
        get { lock (_stateLock) return _safeZ; }
        private set { lock (_stateLock) _safeZ = value; }
    }

    private bool _pendingVacuumCommand;
    public bool PendingVacuumCommand
    {
        get { lock (_stateLock) return _pendingVacuumCommand; }
        private set { lock (_stateLock) _pendingVacuumCommand = value; }
    }

    private DateTime? _vacuumCommandStartedAtUtc;
    public DateTime? VacuumCommandStartedAtUtc
    {
        get { lock (_stateLock) return _vacuumCommandStartedAtUtc; }
        private set { lock (_stateLock) _vacuumCommandStartedAtUtc = value; }
    }

    public bool VacuumSuccessLogged { get; set; }
    public bool VacuumTimeoutLogged { get; set; }
    public bool VacuumConflictLogged { get; set; }

    public ObservableCollection<WorkHeadPosition> Positions { get; } = new();
    public string? SelectedPositionName { get; set; }

    public void UpdateMetadata(string name, string description, int xAxisNo, int yAxisNo, int zAxisNo, int rAxisNo, int vacuumOutputAddress, int blowOutputAddress, int vacuumInputAddress, int generalOutputAddress1, int generalOutputAddress2, int generalInputAddress1, int generalInputAddress2, int vacuumTimeoutMs, double safeZ)
    {
        lock (_stateLock)
        {
            _name = name;
            _description = description;
            _xAxisNo = xAxisNo;
            _yAxisNo = yAxisNo;
            _zAxisNo = zAxisNo;
            _rAxisNo = rAxisNo;
            _vacuumOutputAddress = vacuumOutputAddress;
            _blowOutputAddress = blowOutputAddress;
            _vacuumInputAddress = vacuumInputAddress;
            _generalOutputAddress1 = generalOutputAddress1;
            _generalOutputAddress2 = generalOutputAddress2;
            _generalInputAddress1 = generalInputAddress1;
            _generalInputAddress2 = generalInputAddress2;
            _vacuumTimeoutMs = vacuumTimeoutMs;
            _safeZ = safeZ;
        }
    }

    public void AddPosition(WorkHeadPosition position) => Positions.Add(position);

    public void RemovePosition(string positionName)
    {
        var targets = Positions.Where(p => p.Name == positionName).ToList();
        foreach (var target in targets) Positions.Remove(target);
    }

    public void StartVacuumCommand()
    {
        lock (_stateLock)
        {
            _pendingVacuumCommand = true;
            _vacuumCommandStartedAtUtc = DateTime.UtcNow;
        }
        VacuumSuccessLogged = false;
        VacuumTimeoutLogged = false;
        VacuumConflictLogged = false;
    }

    public void StopVacuumCommand()
    {
        lock (_stateLock)
        {
            _pendingVacuumCommand = false;
            _vacuumCommandStartedAtUtc = null;
        }
        VacuumSuccessLogged = false;
        VacuumTimeoutLogged = false;
        VacuumConflictLogged = false;
    }

    public bool HasVacuumTimedOut(DateTime utcNow)
    {
        lock (_stateLock)
        {
            return _pendingVacuumCommand && _vacuumCommandStartedAtUtc.HasValue
                && utcNow - _vacuumCommandStartedAtUtc.Value >= TimeSpan.FromMilliseconds(_vacuumTimeoutMs);
        }
    }
}
